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
    /// Shares and cash held through the exchange. Personal accounts belong to an ID card, cargo's to the station.
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
        /// Spesos deposited and not yet paid out. Personal accounts only; cargo trades straight from its budget.
        /// </summary>
        public int Cash;

        /// <summary>
        /// Shares per company, with what was paid for the ones still held.
        /// </summary>
        public readonly Dictionary<string, (int Shares, int Cost)> Holdings = new();

        public TimeSpan NextTrade;
        public int Paid;
        public int Received;
    }

    private readonly Dictionary<EntityUid, ExchangeAccount> _accounts = new();
    private readonly Dictionary<EntityUid, ExchangeAccountKind> _selected = new();
    private readonly List<string> _tradeLog = new();
    private int _paidIn;
    private int _paidOut;

    /// <summary>
    /// Whether the Guild has stopped paying out because the round's profit pool is used up.
    /// </summary>
    public bool Halted => _paidOut - _paidIn >= _settings.ProfitPool;

    /// <summary>
    /// Spesos paid out beyond what was paid in this round.
    /// </summary>
    public int NetPayout => _paidOut - _paidIn;

    private void ResetAccounts()
    {
        _accounts.Clear();
        _selected.Clear();
        _tradeLog.Clear();
        _paidIn = 0;
        _paidOut = 0;
    }

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<GuildExchangeComponent> ent, ref BoundUIOpenedEvent args)
    {
        _ui.SetUiState(ent.Owner, GuildExchangeUiKey.Key, BuildState());
        SendAccount(ent, args.Actor, null);
    }

    [SubscribeLocalEvent]
    private void OnSelectAccount(Entity<GuildExchangeComponent> ent, ref GuildExchangeSelectAccountMessage args)
    {
        _selected[args.Actor] = args.Kind;
        SendAccount(ent, args.Actor, null);
    }

    [SubscribeLocalEvent]
    private void OnTrade(Entity<GuildExchangeComponent> ent, ref GuildExchangeTradeMessage args)
    {
        var kind = _selected.GetValueOrDefault(args.Actor, ExchangeAccountKind.Personal);
        var notice = TryTrade(ent, args.Actor, kind, args.Company, args.Shares);
        _ui.SetUiState(ent.Owner, GuildExchangeUiKey.Key, BuildState());
        SendAccount(ent, args.Actor, notice);
    }

    [SubscribeLocalEvent]
    private void OnFullBudget(Entity<GuildExchangeComponent> ent, ref GuildExchangeFullBudgetMessage args)
    {
        SendAccount(ent, args.Actor, SetFullBudget(ent, args.Actor, args.Enabled));
    }

    [SubscribeLocalEvent]
    private void OnUiClosed(Entity<GuildExchangeComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.FullBudgetBy == args.Actor)
            Relock(ent, args.Actor);
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
    private void OnWithdraw(Entity<GuildExchangeComponent> ent, ref GuildExchangeWithdrawMessage args)
    {
        SendAccount(ent, args.Actor, Withdraw(ent, args.Actor));
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
        if (!_idCard.TryFindIdCard(user, out var id))
            return null;

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

    /// <summary>
    /// Buys (positive) or sells (negative) shares for the actor. Returns why it failed, or null if it went through.
    /// </summary>
    public string? TryTrade(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, string companyId, int shares)
    {
        if (Market is not { } market || !market.Companies.TryGetValue(companyId, out var company) || shares == 0)
            return Loc.GetString("guild-exchange-closed");

        var station = EntityUid.Invalid;
        var account = kind == ExchangeAccountKind.Cargo ? GetCargoAccount(ent, out station) : GetPersonalAccount(actor);
        if (account == null)
            return Loc.GetString(kind == ExchangeAccountKind.Cargo ? "guild-exchange-no-station" : "guild-exchange-no-id");

        if (kind == ExchangeAccountKind.Cargo && !_access.IsAllowed(actor, ent))
            return Loc.GetString("guild-exchange-no-access");

        if (_timing.CurTime < account.NextTrade)
            return Loc.GetString("guild-exchange-cooldown");

        // The unlock belongs to the quartermaster who made it; anyone else trading cargo locks it again.
        var fullBudget = false;
        if (kind == ExchangeAccountKind.Cargo && ent.Comp.FullBudgetBy is { } unlockedBy)
        {
            if (unlockedBy == actor && HasFullBudgetAccess(ent, actor))
                fullBudget = true;
            else
                Relock(ent, actor);
        }

        var proto = company.Proto;
        var held = account.Holdings.GetValueOrDefault(companyId);
        int money;
        string verb;
        float newPrice;

        if (shares > 0)
        {
            (money, newPrice) = ExchangeMath.Buy(company.Price, shares, proto.Liquidity, _settings.Spread, _settings.Fee);
            if (account.Cargo)
            {
                var balance = _cargo.GetBalanceFromAccount(station, ent.Comp.Account);
                if (money > balance)
                    return Loc.GetString("guild-exchange-no-funds");
                if (!fullBudget && money > CargoCapLeft(account, balance))
                    return Loc.GetString("guild-exchange-cap");
                _cargo.UpdateBankAccount(station, -money, ent.Comp.Account);
            }
            else
            {
                if (money > account.Cash)
                    return Loc.GetString("guild-exchange-no-funds");
                account.Cash -= money;
            }

            account.Holdings[companyId] = (held.Shares + shares, held.Cost + money);
            account.Paid += money;
            _paidIn += money;
            verb = "bought";
        }
        else
        {
            var selling = -shares;
            if (selling > held.Shares)
                return Loc.GetString("guild-exchange-not-enough-shares");

            (money, newPrice) = ExchangeMath.Sell(company.Price, selling, proto.Liquidity, _settings.Spread, _settings.Fee);
            if (_paidOut + money - _paidIn > _settings.ProfitPool)
                return Loc.GetString("guild-exchange-halted");

            if (account.Cargo)
                _cargo.UpdateBankAccount(station, money, ent.Comp.Account);
            else
                account.Cash += money;

            var costLeft = held.Shares == selling ? 0 : held.Cost - held.Cost * selling / held.Shares;
            if (held.Shares == selling)
                account.Holdings.Remove(companyId);
            else
                account.Holdings[companyId] = (held.Shares - selling, costLeft);
            account.Received += money;
            _paidOut += money;
            verb = "sold";
        }

        company.Price = newPrice;
        account.NextTrade = _timing.CurTime + _settings.TradeCooldown;

        var trader = account.Cargo ? Loc.GetString("guild-exchange-cargo-trader") : account.Holder;
        _tradeLog.Insert(0, Loc.GetString(shares > 0 ? "guild-exchange-log-bought" : "guild-exchange-log-sold",
            ("trader", trader), ("shares", Math.Abs(shares)), ("ticker", proto.Ticker), ("money", money)));
        if (_tradeLog.Count > _settings.TradeLogKept)
            _tradeLog.RemoveAt(_tradeLog.Count - 1);

        _adminLog.Add(LogType.Action, fullBudget ? LogImpact.Medium : LogImpact.Low,
            $"{ToPrettyString(actor):user} {verb} {Math.Abs(shares)} {proto.Ticker} for {money} spesos on the {(account.Cargo ? fullBudget ? "cargo (whole budget unlocked)" : "cargo" : "personal")} account at {ToPrettyString(ent):machine}");
        return null;
    }

    /// <summary>
    /// How much more of the cargo budget may go into shares.
    /// </summary>
    private int CargoCapLeft(ExchangeAccount account, int balance)
    {
        var invested = account.Holdings.Values.Sum(h => h.Cost);
        return Math.Max(0, (int) (_settings.CargoCap * (balance + invested)) - invested);
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

        _stack.SpawnAtPosition(account.Cash, ent.Comp.CashStack, Transform(ent).Coordinates);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(actor):user} withdrew {account.Cash} spesos from their Guild Exchange account at {ToPrettyString(ent):machine}");
        account.Cash = 0;
        return null;
    }

    private void SendAccount(Entity<GuildExchangeComponent> ent, EntityUid actor, string? notice)
    {
        var canTradeCargo = _access.IsAllowed(actor, ent);
        var kind = _selected.GetValueOrDefault(actor, ExchangeAccountKind.Personal);
        if (kind == ExchangeAccountKind.Cargo && !canTradeCargo)
            kind = ExchangeAccountKind.Personal;

        var personal = GetPersonalAccount(actor);
        ExchangeAccount? account;
        int cash;
        int? capLeft = null;
        var fullBudget = ent.Comp.FullBudgetBy == actor;
        if (kind == ExchangeAccountKind.Cargo && GetCargoAccount(ent, out var station) is { } cargo)
        {
            account = cargo;
            cash = _cargo.GetBalanceFromAccount(station, ent.Comp.Account);
            capLeft = fullBudget ? null : CargoCapLeft(cargo, cash);
        }
        else
        {
            account = personal;
            cash = personal?.Cash ?? 0;
        }

        var holdings = account?.Holdings.Select(h => new ExchangeHoldingState(h.Key, h.Value.Shares, h.Value.Cost)).ToList()
            ?? new List<ExchangeHoldingState>();
        var profit = account == null ? 0 : account.Received - account.Paid + HoldingsValue(account);

        _ui.ServerSendUiMessage(ent.Owner, GuildExchangeUiKey.Key,
            new GuildExchangeAccountMessage(kind, personal != null, canTradeCargo, cash, capLeft, holdings, profit, notice,
                canTradeCargo && HasFullBudgetAccess(ent, actor), fullBudget),
            actor);
    }

    /// <summary>
    /// What an account's shares are worth at today's prices.
    /// </summary>
    private int HoldingsValue(ExchangeAccount account)
    {
        if (Market is not { } market)
            return 0;

        return (int) account.Holdings.Sum(h => market.Companies.TryGetValue(h.Key, out var c) ? c.Price * h.Value.Shares : 0f);
    }

    /// <summary>
    /// Cargo's shares are sold at the closing price into its budget; personal cash nobody collected stays with the Guild.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        if (Market is not { } market || _accounts.Count == 0)
            return;

        foreach (var (owner, account) in _accounts)
        {
            if (!account.Cargo || account.Paid == 0)
                continue;

            var total = 0;
            foreach (var (companyId, holding) in account.Holdings)
            {
                if (!market.Companies.TryGetValue(companyId, out var company))
                    continue;
                var (proceeds, newPrice) = ExchangeMath.Sell(company.Price, holding.Shares, company.Proto.Liquidity, _settings.Spread, _settings.Fee);
                proceeds = Math.Clamp(_settings.ProfitPool - (_paidOut - _paidIn), 0, proceeds);
                company.Price = newPrice;
                total += proceeds;
                _paidOut += proceeds;
            }

            account.Holdings.Clear();
            account.Received += total;
            if (total > 0 && HasComp<StationBankAccountComponent>(owner))
                _cargo.UpdateBankAccount(owner, total, account.CargoAccount);

            args.AddLine(Loc.GetString("guild-exchange-round-end-cargo",
                ("station", account.Holder), ("profit", account.Received - account.Paid)));
        }

        var best = _accounts.Values.Where(a => !a.Cargo && a.Paid > 0).MaxBy(a => a.Received - a.Paid);
        if (best != null)
            args.AddLine(Loc.GetString("guild-exchange-round-end-best", ("name", best.Holder), ("profit", best.Received - best.Paid)));
    }
}
