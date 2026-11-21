using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Cargo.Exchange;

/// <summary>
/// Where an account's spesos live: deposited cash for a personal account, the station budget for cargo.
/// </summary>
public interface IExchangeWallet
{
    long Balance { get; }

    void Add(long amount);
}

public sealed class ExchangeLong
{
    public int Shares;

    /// <summary>
    /// What was paid for the shares still held.
    /// </summary>
    public long Cost;
}

/// <summary>
/// Borrowed shares sold short. The sale proceeds plus the account's margin are held as collateral until it's covered.
/// </summary>
public sealed class ExchangeShort
{
    public int Shares;
    public long Collateral;

    /// <summary>
    /// The account's own money in the collateral.
    /// </summary>
    public long Margin;

    /// <summary>
    /// What the short sales paid.
    /// </summary>
    public long Proceeds;

    public long BorrowPaid;

    /// <summary>
    /// Borrowing fees run up but not yet charged, under one spesos.
    /// </summary>
    public float BorrowOwed;
}

public sealed class ExchangeLimitOrder(int id, string company, bool buy, int shares, float limit, EntityUid placedBy)
{
    public readonly int Id = id;
    public readonly string Company = company;
    public readonly bool Buy = buy;
    public readonly int Shares = shares;
    public readonly float Limit = limit;
    public readonly EntityUid PlacedBy = placedBy;
}

/// <summary>
/// Shares, shorts and resting orders held through the exchange. Personal accounts belong to an ID card, cargo's to the
/// station.
/// </summary>
public sealed class ExchangeAccount(bool cargo, string holder)
{
    public readonly bool Cargo = cargo;
    public string Holder = holder;

    /// <summary>
    /// The station account a cargo account trades through.
    /// </summary>
    public ProtoId<CargoAccountPrototype> CargoAccount = "Cargo";

    /// <summary>
    /// Who has the whole cargo budget unlocked, on any terminal. It locks again when they toggle it off, close the
    /// window, or someone else trades the cargo account.
    /// </summary>
    public EntityUid? FullBudgetBy;

    /// <summary>
    /// Spesos deposited and not yet paid out. Personal accounts only; cargo trades straight from its budget.
    /// </summary>
    public long Cash;

    public readonly Dictionary<string, ExchangeLong> Longs = new();
    public readonly Dictionary<string, ExchangeShort> Shorts = new();
    public readonly List<ExchangeLimitOrder> Orders = new();
    public int NextOrderId = 1;
    public TimeSpan NextTrade;

    /// <summary>
    /// Profit and loss on closed trades, dividends and borrowing fees.
    /// </summary>
    public long Realised;

    /// <summary>
    /// Whether this account has ever traded.
    /// </summary>
    public bool Traded;

    /// <summary>
    /// Losses a forced cover couldn't collect because the account ran dry.
    /// </summary>
    public long WrittenOff;
}

/// <summary>
/// A finished trade, or why it didn't happen. <see cref="Error"/> is a locale id.
/// </summary>
public readonly record struct ExchangeTradeResult(string? Error, long Money = 0, int Shares = 0)
{
    public bool Ok => Error == null;

    public static ExchangeTradeResult Fail(string error) => new(error);
}

/// <summary>
/// The trading rules. Every spesos moves through the account's wallet or a short's collateral.
/// </summary>
public static class ExchangeLedger
{
    /// <summary>
    /// Spesos of an account's budget at stake: what it paid for shares and what its shorts sold.
    /// </summary>
    public static long Exposure(ExchangeAccount account)
    {
        return account.Longs.Values.Sum(l => l.Cost) + account.Shorts.Values.Sum(s => s.Proceeds);
    }

    /// <summary>
    /// How much more of a budget may go into shares and shorts under a cap on budget plus money already put in.
    /// </summary>
    public static long CapLeft(ExchangeAccount account, long balance, float cap)
    {
        var putIn = account.Longs.Values.Sum(l => l.Cost) + account.Shorts.Values.Sum(s => s.Margin);
        return Math.Max(0, (long) (cap * (balance + putIn)) - Exposure(account));
    }

    private static bool TryGetCompany(ExchangeMarket market, string companyId, int shares,
        [NotNullWhen(true)] out ExchangeMarket.Company? company)
    {
        return market.Companies.TryGetValue(companyId, out company) && shares > 0;
    }

    /// <summary>
    /// Buys shares at the ask, fee on top.
    /// </summary>
    public static ExchangeTradeResult Buy(ExchangeMarket market, ExchangeAccount account, IExchangeWallet wallet,
        string companyId, int shares, long? capLeft = null)
    {
        if (!TryGetCompany(market, companyId, shares, out var company))
            return ExchangeTradeResult.Fail("guild-exchange-closed");
        if (account.Shorts.ContainsKey(companyId))
            return ExchangeTradeResult.Fail("guild-exchange-cover-first");

        var cost = ExchangeMath.Buy(company.Price, shares, company.Proto.Spread, market.Settings.Fee);
        if (cost > wallet.Balance)
            return ExchangeTradeResult.Fail("guild-exchange-no-funds");
        if (capLeft is { } cap && cost > cap)
            return ExchangeTradeResult.Fail("guild-exchange-cap");

        wallet.Add(-cost);
        var held = account.Longs.GetValueOrDefault(companyId) ?? (account.Longs[companyId] = new ExchangeLong());
        held.Shares += shares;
        held.Cost += cost;
        account.Traded = true;
        market.AddVolume(company, shares);
        return new ExchangeTradeResult(null, cost, shares);
    }

    /// <summary>
    /// Sells held shares at the bid, fee taken off.
    /// </summary>
    public static ExchangeTradeResult Sell(ExchangeMarket market, ExchangeAccount account, IExchangeWallet wallet,
        string companyId, int shares)
    {
        if (!TryGetCompany(market, companyId, shares, out var company))
            return ExchangeTradeResult.Fail("guild-exchange-closed");
        if (!account.Longs.TryGetValue(companyId, out var held) || held.Shares < shares)
            return ExchangeTradeResult.Fail("guild-exchange-not-enough-shares");

        var proceeds = ExchangeMath.Sell(company.Price, shares, company.Proto.Spread, market.Settings.Fee);
        var costPart = held.Cost * shares / held.Shares;
        held.Shares -= shares;
        held.Cost -= costPart;
        if (held.Shares == 0)
            account.Longs.Remove(companyId);

        wallet.Add(proceeds);
        account.Realised += proceeds - costPart;
        account.Traded = true;
        market.AddVolume(company, shares);
        return new ExchangeTradeResult(null, proceeds, shares);
    }

    /// <summary>
    /// Borrows shares and sells them. The proceeds and a margin from the wallet are held as collateral.
    /// </summary>
    public static ExchangeTradeResult Short(ExchangeMarket market, ExchangeAccount account, IExchangeWallet wallet,
        string companyId, int shares, long? capLeft = null)
    {
        if (!TryGetCompany(market, companyId, shares, out var company))
            return ExchangeTradeResult.Fail("guild-exchange-closed");
        if (account.Longs.ContainsKey(companyId))
            return ExchangeTradeResult.Fail("guild-exchange-sell-first");

        var proceeds = ExchangeMath.Sell(company.Price, shares, company.Proto.Spread, market.Settings.Fee);
        var cover = ExchangeMath.Buy(company.Price, shares, company.Proto.Spread, market.Settings.Fee);
        // Rounding eats small shorts, so hold enough that one never starts out under the maintenance margin.
        var margin = Math.Max((long) MathF.Ceiling(market.Settings.ShortMargin * company.Price * shares),
            (long) MathF.Ceiling(market.Settings.MaintenanceMargin * cover) - proceeds + 1);
        if (margin > wallet.Balance)
            return ExchangeTradeResult.Fail("guild-exchange-no-funds");
        if (capLeft is { } cap && proceeds > cap)
            return ExchangeTradeResult.Fail("guild-exchange-cap");

        wallet.Add(-margin);
        var held = account.Shorts.GetValueOrDefault(companyId) ?? (account.Shorts[companyId] = new ExchangeShort());
        held.Shares += shares;
        held.Collateral += proceeds + margin;
        held.Margin += margin;
        held.Proceeds += proceeds;
        account.Traded = true;
        market.AddVolume(company, shares);
        return new ExchangeTradeResult(null, proceeds, shares);
    }

    /// <summary>
    /// Buys borrowed shares back out of their collateral. If that falls short, the wallet makes up what it can.
    /// </summary>
    public static ExchangeTradeResult Cover(ExchangeMarket market, ExchangeAccount account, IExchangeWallet wallet,
        string companyId, int shares)
    {
        if (!TryGetCompany(market, companyId, shares, out var company))
            return ExchangeTradeResult.Fail("guild-exchange-closed");
        if (!account.Shorts.TryGetValue(companyId, out var held) || held.Shares < shares)
            return ExchangeTradeResult.Fail("guild-exchange-not-enough-short");

        var cost = ExchangeMath.Buy(company.Price, shares, company.Proto.Spread, market.Settings.Fee);
        var collateral = held.Collateral * shares / held.Shares;
        var margin = held.Margin * shares / held.Shares;
        var proceeds = held.Proceeds * shares / held.Shares;
        held.Shares -= shares;
        held.Collateral -= collateral;
        held.Margin -= margin;
        held.Proceeds -= proceeds;
        if (held.Shares == 0)
            account.Shorts.Remove(companyId);

        var back = collateral - cost;
        if (back < 0)
        {
            var taken = Math.Min(-back, Math.Max(0, wallet.Balance));
            account.WrittenOff += -back - taken;
            back = -taken;
        }

        wallet.Add(back);
        account.Realised += collateral - margin - cost;
        account.Traded = true;
        market.AddVolume(company, shares);
        return new ExchangeTradeResult(null, cost, shares);
    }

    /// <summary>
    /// Charges a tick's borrowing fee on every short, out of its collateral.
    /// </summary>
    public static void ChargeBorrowFees(ExchangeMarket market, ExchangeAccount account)
    {
        foreach (var (companyId, held) in account.Shorts)
        {
            if (!market.Companies.TryGetValue(companyId, out var company))
                continue;
            held.BorrowOwed += market.Settings.BorrowFee * company.Price * held.Shares;
            var fee = (long) held.BorrowOwed;
            held.BorrowOwed -= fee;
            held.Collateral -= fee;
            held.BorrowPaid += fee;
        }
    }

    /// <summary>
    /// Collateral over the cost of covering now. Below the maintenance margin the short is covered by force.
    /// </summary>
    public static float MarginHealth(ExchangeMarket market, string companyId, ExchangeShort held)
    {
        var company = market.Companies[companyId];
        var cost = ExchangeMath.Buy(company.Price, held.Shares, company.Proto.Spread, market.Settings.Fee);
        return held.Collateral / (float) Math.Max(1, cost);
    }

    /// <summary>
    /// Covers every short below the maintenance margin. Returns what was covered.
    /// </summary>
    public static List<(string Company, ExchangeTradeResult Result)> MarginCalls(ExchangeMarket market, ExchangeAccount account,
        IExchangeWallet wallet)
    {
        var called = new List<(string, ExchangeTradeResult)>();
        foreach (var (companyId, held) in account.Shorts.ToList())
        {
            if (MarginHealth(market, companyId, held) >= market.Settings.MaintenanceMargin)
                continue;
            called.Add((companyId, Cover(market, account, wallet, companyId, held.Shares)));
        }

        return called;
    }

    /// <summary>
    /// Pays a dividend to holders; shorts pay it out of their collateral, as a borrower does.
    /// </summary>
    public static void PayDividend(ExchangeAccount account, IExchangeWallet wallet, string companyId, float amount)
    {
        if (account.Longs.TryGetValue(companyId, out var held))
        {
            var paid = (long) MathF.Floor(held.Shares * amount);
            wallet.Add(paid);
            account.Realised += paid;
        }

        if (account.Shorts.TryGetValue(companyId, out var shorted))
            shorted.Collateral -= (long) MathF.Ceiling(shorted.Shares * amount);
    }

    /// <summary>
    /// Leaves an order to buy or sell once the price reaches <paramref name="limit"/>.
    /// </summary>
    public static ExchangeTradeResult PlaceLimit(ExchangeMarket market, ExchangeAccount account, string companyId, bool buy,
        int shares, float limit, EntityUid placedBy)
    {
        if (!TryGetCompany(market, companyId, shares, out _) || !float.IsFinite(limit) || limit <= 0f)
            return ExchangeTradeResult.Fail("guild-exchange-bad-order");
        if (account.Orders.Count >= market.Settings.MaxOpenOrders)
            return ExchangeTradeResult.Fail("guild-exchange-too-many-orders");

        account.Orders.Add(new ExchangeLimitOrder(account.NextOrderId++, companyId, buy, shares, limit, placedBy));
        return new ExchangeTradeResult(null, 0, shares);
    }

    /// <summary>
    /// Drops a resting order. Returns whether there was one.
    /// </summary>
    public static bool CancelLimit(ExchangeAccount account, int id)
    {
        return account.Orders.RemoveAll(o => o.Id == id) > 0;
    }

    /// <summary>
    /// Fills resting orders whose price has been reached. Each fills whole at the market or is cancelled if it can't.
    /// </summary>
    public static List<(ExchangeLimitOrder Order, ExchangeTradeResult Result)> FillLimits(ExchangeMarket market,
        ExchangeAccount account, IExchangeWallet wallet, Func<long?> capLeft)
    {
        var done = new List<(ExchangeLimitOrder, ExchangeTradeResult)>();
        foreach (var order in account.Orders.ToList())
        {
            if (!market.Companies.TryGetValue(order.Company, out var company))
                continue;

            var reached = order.Buy
                ? ExchangeMath.Ask(company.Price, company.Proto.Spread) <= order.Limit
                : ExchangeMath.Bid(company.Price, company.Proto.Spread) >= order.Limit;
            if (!reached)
                continue;

            var result = order.Buy
                ? Buy(market, account, wallet, order.Company, order.Shares, capLeft())
                : Sell(market, account, wallet, order.Company, order.Shares);
            account.Orders.Remove(order);
            done.Add((order, result));
        }

        return done;
    }

    /// <summary>
    /// Closes everything: shorts are covered, shares sold and resting orders dropped.
    /// </summary>
    public static void Settle(ExchangeMarket market, ExchangeAccount account, IExchangeWallet wallet)
    {
        account.Orders.Clear();
        foreach (var (companyId, held) in account.Shorts.ToList())
        {
            Cover(market, account, wallet, companyId, held.Shares);
        }

        foreach (var (companyId, held) in account.Longs.ToList())
        {
            Sell(market, account, wallet, companyId, held.Shares);
        }
    }

    /// <summary>
    /// What the account's open positions would bring in if closed now, before trading costs.
    /// </summary>
    public static long Unrealised(ExchangeMarket market, ExchangeAccount account)
    {
        long total = 0;
        foreach (var (companyId, held) in account.Longs)
        {
            if (market.Companies.TryGetValue(companyId, out var company))
                total += (long) (ExchangeMath.Bid(company.Price, company.Proto.Spread) * held.Shares) - held.Cost;
        }

        foreach (var (companyId, held) in account.Shorts)
        {
            if (market.Companies.TryGetValue(companyId, out var company))
                total += held.Collateral - held.Margin - (long) (ExchangeMath.Ask(company.Price, company.Proto.Spread) * held.Shares);
        }

        return total;
    }
}
