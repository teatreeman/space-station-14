using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Cargo.Exchange;

public sealed partial class GuildExchangeSystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StationSystem _station = default!;

    /// <summary>
    /// Accounts by owner: an ID card for personal accounts, the station for cargo's.
    /// </summary>
    private readonly Dictionary<EntityUid, ExchangeAccount> _accounts = new();
    private readonly Dictionary<EntityUid, ExchangeAccountKind> _selected = new();
    private readonly Dictionary<EntityUid, string> _watching = new();
    private readonly List<ExchangeTradeLogState> _tradeLog = new();

    private sealed class CashWallet(ExchangeAccount account) : IExchangeWallet
    {
        public long Balance => account.Cash;

        public void Add(long amount) => account.Cash += amount;
    }

    private sealed class BankWallet(CargoSystem cargo, EntityUid station, ProtoId<CargoAccountPrototype> bank) : IExchangeWallet
    {
        public long Balance => cargo.GetBalanceFromAccount(station, bank);

        public void Add(long amount) => cargo.UpdateBankAccount(station, (int) Math.Clamp(amount, int.MinValue, int.MaxValue), bank);
    }

    private void ResetAccounts()
    {
        _accounts.Clear();
        _selected.Clear();
        _watching.Clear();
        _tradeLog.Clear();
    }

    private IExchangeWallet WalletOf(EntityUid owner, ExchangeAccount account)
    {
        return account.Cargo ? new BankWallet(_cargo, owner, account.CargoAccount) : new CashWallet(account);
    }

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<GuildExchangeComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (Market is { } market && !_watching.ContainsKey(args.Actor) && market.Companies.Count > 0)
            _watching[args.Actor] = market.Companies.Keys.First();

        _ui.SetUiState(ent.Owner, GuildExchangeUiKey.Key, BuildState());
        SendAccount(ent, args.Actor, null);
        SendChart(ent, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnUiClosed(Entity<GuildExchangeComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.FullBudgetBy == args.Actor)
            Relock(ent, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnSelectAccount(Entity<GuildExchangeComponent> ent, ref GuildExchangeSelectAccountMessage args)
    {
        _selected[args.Actor] = args.Kind;
        SendAccount(ent, args.Actor, null);
    }

    [SubscribeLocalEvent]
    private void OnSelectCompany(Entity<GuildExchangeComponent> ent, ref GuildExchangeSelectCompanyMessage args)
    {
        if (Market is not { } market || !market.Companies.ContainsKey(args.Company))
            return;

        _watching[args.Actor] = args.Company;
        SendChart(ent, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnOrder(Entity<GuildExchangeComponent> ent, ref GuildExchangeOrderMessage args)
    {
        var kind = _selected.GetValueOrDefault(args.Actor, ExchangeAccountKind.Personal);
        var notice = args.Type == ExchangeOrderType.Limit
            ? TryPlaceLimit(ent, args.Actor, kind, args.Company, args.Side == ExchangeOrderSide.Buy, args.Shares, args.Limit)
            : TryOrder(ent, args.Actor, kind, args.Side, args.Company, args.Shares);
        RefreshAll();
        SendAccount(ent, args.Actor, notice);
    }

    [SubscribeLocalEvent]
    private void OnCancelOrder(Entity<GuildExchangeComponent> ent, ref GuildExchangeCancelOrderMessage args)
    {
        var kind = _selected.GetValueOrDefault(args.Actor, ExchangeAccountKind.Personal);
        if (AccountFor(ent, args.Actor, kind, out _) is { } account && ExchangeLedger.CancelLimit(account, args.Id))
        {
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(args.Actor):user} cancelled limit order {args.Id} on the {(account.Cargo ? "cargo" : "personal")} exchange account at {ToPrettyString(ent):machine}");
        }

        SendAccount(ent, args.Actor, null);
    }

    [SubscribeLocalEvent]
    private void OnFullBudget(Entity<GuildExchangeComponent> ent, ref GuildExchangeFullBudgetMessage args)
    {
        SendAccount(ent, args.Actor, SetFullBudget(ent, args.Actor, args.Enabled));
    }

    [SubscribeLocalEvent]
    private void OnWithdraw(Entity<GuildExchangeComponent> ent, ref GuildExchangeWithdrawMessage args)
    {
        SendAccount(ent, args.Actor, Withdraw(ent, args.Actor));
    }

    /// <summary>
    /// Whether the actor carries the access that can unlock the whole cargo budget.
    /// </summary>
    public bool HasFullBudgetAccess(Entity<GuildExchangeComponent> ent, EntityUid actor)
    {
        return _access.FindAccessTags(actor).Contains(ent.Comp.FullBudgetAccess);
    }

    /// <summary>
    /// Unlocks the whole cargo budget for the actor, or locks it again. Returns why it failed, or null.
    /// </summary>
    public string? SetFullBudget(Entity<GuildExchangeComponent> ent, EntityUid actor, bool enabled)
    {
        if (!enabled)
        {
            if (ent.Comp.FullBudgetBy != null)
                Relock(ent, actor);
            return null;
        }

        if (!HasFullBudgetAccess(ent, actor))
            return Loc.GetString("guild-exchange-full-budget-denied");

        ent.Comp.FullBudgetBy = actor;
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):user} unlocked the whole cargo budget for trading at {ToPrettyString(ent):machine}");
        return null;
    }

    private void Relock(Entity<GuildExchangeComponent> ent, EntityUid actor)
    {
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"Whole-budget trading at {ToPrettyString(ent):machine} locked again ({ToPrettyString(actor):user})");
        ent.Comp.FullBudgetBy = null;
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<GuildExchangeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != ent.Comp.CashStack)
            return;

        args.Handled = true;
        if (GetPersonalAccount(args.User) is not { } account)
        {
            _popup.PopupEntity(Loc.GetString("guild-exchange-no-id"), ent, args.User);
            return;
        }

        account.Cash += stack.Count;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):user} deposited {stack.Count} spesos into their Guild Exchange account at {ToPrettyString(ent):machine}");
        _popup.PopupEntity(Loc.GetString("guild-exchange-deposited", ("amount", stack.Count)), ent, args.User);
        QueueDel(args.Used);
        SendAccount(ent, args.User, null);
    }

    /// <summary>
    /// The personal account of whoever carries this ID card, made on first use.
    /// </summary>
    public ExchangeAccount? GetPersonalAccount(EntityUid user)
    {
        return GetPersonalAccount(user, out _);
    }

    private ExchangeAccount? GetPersonalAccount(EntityUid user, out EntityUid owner)
    {
        owner = default;
        if (!_idCard.TryFindIdCard(user, out var id))
            return null;

        owner = id;
        if (!_accounts.TryGetValue(id, out var account))
        {
            account = new ExchangeAccount(false, id.Comp.FullName ?? Name(user));
            _accounts[id] = account;
        }

        return account;
    }

    /// <summary>
    /// The station's cargo account, if this machine is on a station with a budget.
    /// </summary>
    public ExchangeAccount? GetCargoAccount(EntityUid machine, out EntityUid station)
    {
        station = default;
        if (_station.GetOwningStation(machine) is not { } owner || !HasComp<StationBankAccountComponent>(owner))
            return null;

        station = owner;
        if (!_accounts.TryGetValue(owner, out var account))
        {
            account = new ExchangeAccount(true, Name(owner)) { CargoAccount = Comp<GuildExchangeComponent>(machine).Account };
            _accounts[owner] = account;
        }

        return account;
    }

    private ExchangeAccount? AccountFor(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, out EntityUid owner)
    {
        if (kind == ExchangeAccountKind.Cargo)
            return _access.IsAllowed(actor, ent) ? GetCargoAccount(ent, out owner) : AccountMissing(out owner);
        return GetPersonalAccount(actor, out owner);
    }

    private static ExchangeAccount? AccountMissing(out EntityUid owner)
    {
        owner = default;
        return null;
    }

    /// <summary>
    /// Buys (positive) or sells (negative) shares at the market. Returns why it failed, or null if it went through.
    /// </summary>
    public string? TryTrade(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, string companyId, int shares)
    {
        return TryOrder(ent, actor, kind, shares > 0 ? ExchangeOrderSide.Buy : ExchangeOrderSide.Sell, companyId, Math.Abs(shares));
    }

    /// <summary>
    /// A market order: buy, sell, short or cover. Returns why it failed, or null if it went through.
    /// </summary>
    public string? TryOrder(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, ExchangeOrderSide side,
        string companyId, int shares)
    {
        if (Market is not { } market || !market.Companies.TryGetValue(companyId, out var company) || shares <= 0)
            return Loc.GetString("guild-exchange-closed");

        if (CheckAccount(ent, actor, kind, out var owner, out var fullBudget) is { } error)
            return error;

        var account = _accounts[owner];
        var wallet = WalletOf(owner, account);
        long? cap = account.Cargo && !fullBudget ? ExchangeLedger.CapLeft(account, wallet.Balance, _settings.CargoCap) : null;

        var result = side switch
        {
            ExchangeOrderSide.Buy => ExchangeLedger.Buy(market, account, wallet, companyId, shares, cap),
            ExchangeOrderSide.Sell => ExchangeLedger.Sell(market, account, wallet, companyId, shares),
            ExchangeOrderSide.Short => ExchangeLedger.Short(market, account, wallet, companyId, shares, cap),
            _ => ExchangeLedger.Cover(market, account, wallet, companyId, shares),
        };

        if (!result.Ok)
            return Loc.GetString(result.Error!);

        account.NextTrade = _timing.CurTime + _settings.TradeCooldown;
        LogTrade(ent, actor, account, side, company.Proto.Ticker, result, fullBudget ? "whole budget unlocked" : null);
        return null;
    }

    /// <summary>
    /// Leaves a limit order to buy or sell when the price reaches <paramref name="limit"/>.
    /// </summary>
    public string? TryPlaceLimit(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, string companyId,
        bool buy, int shares, float limit)
    {
        if (Market is not { } market || !market.Companies.TryGetValue(companyId, out var company))
            return Loc.GetString("guild-exchange-closed");

        if (CheckAccount(ent, actor, kind, out var owner, out _) is { } error)
            return error;

        var account = _accounts[owner];
        var result = ExchangeLedger.PlaceLimit(market, account, companyId, buy, shares, limit, actor);
        if (!result.Ok)
            return Loc.GetString(result.Error!);

        account.NextTrade = _timing.CurTime + _settings.TradeCooldown;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(actor):user} placed a limit order to {(buy ? "buy" : "sell")} {shares} {company.Proto.Ticker} at {limit:0.00} on the {(account.Cargo ? "cargo" : "personal")} account at {ToPrettyString(ent):machine}");
        return null;
    }

    /// <summary>
    /// Finds the account to trade and checks access and cooldown. Returns why not, or null.
    /// </summary>
    private string? CheckAccount(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, out EntityUid owner,
        out bool fullBudget)
    {
        fullBudget = false;
        if (kind == ExchangeAccountKind.Cargo && !_access.IsAllowed(actor, ent))
        {
            owner = default;
            return Loc.GetString("guild-exchange-no-access");
        }

        var account = AccountFor(ent, actor, kind, out owner);
        if (account == null)
            return Loc.GetString(kind == ExchangeAccountKind.Cargo ? "guild-exchange-no-station" : "guild-exchange-no-id");

        if (_timing.CurTime < account.NextTrade)
            return Loc.GetString("guild-exchange-cooldown");

        // The unlock belongs to the quartermaster who made it; anyone else trading cargo locks it again.
        if (kind == ExchangeAccountKind.Cargo && ent.Comp.FullBudgetBy is { } unlockedBy)
        {
            if (unlockedBy == actor && HasFullBudgetAccess(ent, actor))
                fullBudget = true;
            else
                Relock(ent, actor);
        }

        return null;
    }

    private void LogTrade(EntityUid machine, EntityUid actor, ExchangeAccount account, ExchangeOrderSide side, string ticker,
        ExchangeTradeResult result, string? note)
    {
        var trader = account.Cargo ? Loc.GetString("guild-exchange-cargo-trader") : account.Holder;
        var key = side switch
        {
            ExchangeOrderSide.Buy => "guild-exchange-log-bought",
            ExchangeOrderSide.Sell => "guild-exchange-log-sold",
            ExchangeOrderSide.Short => "guild-exchange-log-shorted",
            _ => "guild-exchange-log-covered",
        };

        AddTradeLog(Loc.GetString(key, ("trader", trader), ("shares", result.Shares), ("ticker", ticker), ("money", result.Money)));
        _adminLog.Add(LogType.Action, note != null ? LogImpact.Medium : LogImpact.Low,
            $"{ToPrettyString(actor):user} {side.ToString().ToLowerInvariant()} {result.Shares} {ticker} for {result.Money} spesos on the {(account.Cargo ? "cargo" : "personal")} account{(note != null ? $" ({note})" : "")} at {ToPrettyString(machine):machine}");
    }

    private void AddTradeLog(string text)
    {
        _tradeLog.Insert(0, new ExchangeTradeLogState(_timing.CurTime, text));
        if (_tradeLog.Count > _settings.TradeLogKept)
            _tradeLog.RemoveAt(_tradeLog.Count - 1);
    }

    /// <summary>
    /// Each tick: borrowing fees, margin calls and limit orders.
    /// </summary>
    private void TickAccounts()
    {
        if (Market is not { } market)
            return;

        foreach (var (owner, account) in _accounts)
        {
            if (account.Shorts.Count == 0 && account.Orders.Count == 0)
                continue;
            if (account.Cargo && !HasComp<StationBankAccountComponent>(owner))
                continue;

            var wallet = WalletOf(owner, account);
            ExchangeLedger.ChargeBorrowFees(market, account);

            foreach (var (companyId, result) in ExchangeLedger.MarginCalls(market, account, wallet))
            {
                var ticker = market.Companies[companyId].Proto.Ticker;
                var trader = account.Cargo ? Loc.GetString("guild-exchange-cargo-trader") : account.Holder;
                AddTradeLog(Loc.GetString("guild-exchange-log-margin-call", ("trader", trader), ("shares", result.Shares),
                    ("ticker", ticker), ("money", result.Money), ("time", StationTime(_timing.CurTime))));
                _adminLog.Add(LogType.Action, LogImpact.Medium,
                    $"Guild Exchange margin call covered {result.Shares} {ticker} for {result.Money} spesos on {account.Holder}'s {(account.Cargo ? "cargo" : "personal")} account; {account.WrittenOff} spesos written off so far");
            }

            foreach (var (order, result) in ExchangeLedger.FillLimits(market, account, wallet,
                         () => account.Cargo ? ExchangeLedger.CapLeft(account, wallet.Balance, _settings.CargoCap) : null))
            {
                var ticker = market.Companies[order.Company].Proto.Ticker;
                if (result.Ok)
                {
                    LogTrade(owner, order.PlacedBy, account, order.Buy ? ExchangeOrderSide.Buy : ExchangeOrderSide.Sell, ticker, result,
                        "limit order");
                }
                else
                {
                    _adminLog.Add(LogType.Action, LogImpact.Low,
                        $"Limit order {order.Id} for {order.Shares} {ticker} on {account.Holder}'s exchange account was cancelled: {result.Error}");
                }
            }
        }
    }

    private void PayDividend(string companyId, float amount)
    {
        foreach (var (owner, account) in _accounts)
        {
            if (!account.Longs.ContainsKey(companyId) && !account.Shorts.ContainsKey(companyId))
                continue;
            if (account.Cargo && !HasComp<StationBankAccountComponent>(owner))
                continue;

            ExchangeLedger.PayDividend(account, WalletOf(owner, account), companyId, amount);
        }
    }

    /// <summary>
    /// Pays out the actor's personal cash as spesos at the machine.
    /// </summary>
    public string? Withdraw(Entity<GuildExchangeComponent> ent, EntityUid actor)
    {
        if (GetPersonalAccount(actor) is not { } account)
            return Loc.GetString("guild-exchange-no-id");
        if (account.Cash <= 0)
            return Loc.GetString("guild-exchange-no-funds");

        var amount = (int) Math.Min(account.Cash, int.MaxValue);
        _stack.SpawnAtPosition(amount, ent.Comp.CashStack, Transform(ent).Coordinates);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(actor):user} withdrew {amount} spesos from their Guild Exchange account at {ToPrettyString(ent):machine}");
        account.Cash -= amount;
        return null;
    }

    private void SendAccount(Entity<GuildExchangeComponent> ent, EntityUid actor, string? notice)
    {
        var canTradeCargo = _access.IsAllowed(actor, ent);
        var kind = _selected.GetValueOrDefault(actor, ExchangeAccountKind.Personal);
        if (kind == ExchangeAccountKind.Cargo && !canTradeCargo)
            kind = ExchangeAccountKind.Personal;

        var personal = GetPersonalAccount(actor);
        var fullBudget = ent.Comp.FullBudgetBy == actor;
        var message = new GuildExchangeAccountMessage
        {
            Kind = kind,
            HasId = personal != null,
            CanTradeCargo = canTradeCargo,
            Notice = notice,
            CanUnlockFullBudget = canTradeCargo && HasFullBudgetAccess(ent, actor),
            FullBudget = fullBudget,
        };

        ExchangeAccount? account = personal;
        message.Cash = personal?.Cash ?? 0;
        if (kind == ExchangeAccountKind.Cargo && GetCargoAccount(ent, out var station) is { } cargo)
        {
            account = cargo;
            message.Cash = _cargo.GetBalanceFromAccount(station, cargo.CargoAccount);
            message.CapLeft = fullBudget ? null : ExchangeLedger.CapLeft(cargo, message.Cash, _settings.CargoCap);
        }

        if (account != null && Market is { } market)
        {
            message.Longs = account.Longs.Select(l => new ExchangePositionState(l.Key, l.Value.Shares, l.Value.Cost)).ToList();
            message.Shorts = account.Shorts.Select(s => new ExchangeShortState(s.Key, s.Value.Shares, s.Value.Collateral,
                s.Value.Margin, s.Value.Proceeds, s.Value.BorrowPaid, ExchangeLedger.MarginHealth(market, s.Key, s.Value))).ToList();
            message.Orders = account.Orders.Select(o => new ExchangeOrderState(o.Id, o.Company, o.Buy, o.Shares, o.Limit)).ToList();
            message.Realised = account.Realised;
            message.Unrealised = ExchangeLedger.Unrealised(market, account);
        }

        _ui.ServerSendUiMessage(ent.Owner, GuildExchangeUiKey.Key, message, actor);
    }

    /// <summary>
    /// The closing bell: every short is covered and every share sold. Cargo's money goes back into its budget; personal
    /// cash nobody collected stays with the Guild.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        if (Market is not { } market || _accounts.Count == 0)
            return;

        args.AddLine(Loc.GetString("guild-exchange-round-end-bell", ("time", StationTime(_timing.CurTime))));
        foreach (var (owner, account) in _accounts)
        {
            if (account.Cargo && !HasComp<StationBankAccountComponent>(owner))
                continue;

            ExchangeLedger.Settle(market, account, WalletOf(owner, account));
            if (account.Cargo && account.Traded)
            {
                args.AddLine(Loc.GetString("guild-exchange-round-end-cargo", ("station", account.Holder),
                    ("profit", account.Realised)));
            }
        }

        var best = _accounts.Values.Where(a => !a.Cargo && a.Traded).MaxBy(a => a.Realised);
        if (best != null)
            args.AddLine(Loc.GetString("guild-exchange-round-end-best", ("name", best.Holder), ("profit", best.Realised)));
    }
}
