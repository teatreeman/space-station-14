using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
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
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StationSystem _station = default!;

    private static readonly TimeSpan MessageDelay = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// Accounts by owner: an ID card for personal accounts, the station for cargo's.
    /// </summary>
    private readonly Dictionary<EntityUid, ExchangeAccount> _accounts = new();
    private readonly Dictionary<EntityUid, ExchangeAccountKind> _selected = new();
    private readonly Dictionary<EntityUid, string> _watching = new();
    private readonly Dictionary<(EntityUid, Type), TimeSpan> _nextMessage = new();

    /// <summary>
    /// Viewers whose answer the throttle held back, with the terminal they used and when to send it.
    /// </summary>
    private readonly Dictionary<EntityUid, (EntityUid Machine, TimeSpan At)> _replyDue = new();

    /// <summary>
    /// The latest full-budget toggle a throttled viewer asked for, applied with their late answer.
    /// </summary>
    private readonly Dictionary<EntityUid, bool> _fullBudgetDue = new();

    private readonly List<ExchangeTradeLogState> _tradeLog = new();

    /// <summary>
    /// The closing bell and results, kept for the round end summary once the market has settled.
    /// </summary>
    private readonly List<string> _closingLines = new();

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
        _nextMessage.Clear();
        _replyDue.Clear();
        _fullBudgetDue.Clear();
        _tradeLog.Clear();
        _closingLines.Clear();
    }

    private IExchangeWallet WalletOf(EntityUid owner, ExchangeAccount account)
    {
        return account.Cargo ? new BankWallet(_cargo, owner, account.CargoAccount) : new CashWallet(account);
    }

    /// <summary>
    /// Whether to skip this message's work for now. With <paramref name="answerLater"/> the viewer still gets their account
    /// and chart once the throttle passes; without it the message was a repeat that was already answered.
    /// </summary>
    private bool Throttled(EntityUid machine, EntityUid actor, Type message, bool answerLater = true)
    {
        var key = (actor, message);
        if (_timing.CurTime < _nextMessage.GetValueOrDefault(key))
        {
            if (answerLater)
                _replyDue[actor] = (machine, _nextMessage[key]);
            return true;
        }

        _nextMessage[key] = _timing.CurTime + MessageDelay;
        return false;
    }

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<GuildExchangeComponent> ent, ref BoundUIOpenedEvent args)
    {
        // Anyone else looking already has the current state.
        if (_ui.GetActors(ent.Owner, GuildExchangeUiKey.Key).Count() == 1)
            _ui.SetUiState(ent.Owner, GuildExchangeUiKey.Key, BuildState());

        // A new window starts on the first company.
        if (_market is { Companies.Count: > 0 } market)
            _watching[args.Actor] = market.Companies.Keys.First();

        // A window opened by prediction is already listening, and its company request may beat this open, so answer now too.
        if (Throttled(ent, args.Actor, typeof(BoundUIOpenedEvent)))
            return;

        SendAccount(ent, args.Actor, null);
        SendChart(ent, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnUiClosed(Entity<GuildExchangeComponent> ent, ref BoundUIClosedEvent args)
    {
        if (GetCargoAccount(ent, out _) is { } cargo && cargo.FullBudgetBy == args.Actor)
            Relock(ent, cargo, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnSelectAccount(Entity<GuildExchangeComponent> ent, ref GuildExchangeSelectAccountMessage args)
    {
        if (!Enum.IsDefined(args.Kind))
            return;

        _selected[args.Actor] = args.Kind;
        if (!Throttled(ent, args.Actor, args.GetType()))
            SendAccount(ent, args.Actor, null);
    }

    [SubscribeLocalEvent]
    private void OnSelectCompany(Entity<GuildExchangeComponent> ent, ref GuildExchangeSelectCompanyMessage args)
    {
        if (_market is not { } market || !market.Companies.ContainsKey(args.Company))
            return;

        _watching[args.Actor] = args.Company;
        if (Throttled(ent, args.Actor, args.GetType()))
            return;

        // The window asks for a company once it has opened. Anything sent to it before that is dropped.
        SendChart(ent, args.Actor);
        SendAccount(ent, args.Actor, null);
    }

    [SubscribeLocalEvent]
    private void OnOrder(Entity<GuildExchangeComponent> ent, ref GuildExchangeOrderMessage args)
    {
        if (Throttled(ent, args.Actor, args.GetType(), answerLater: false))
            return;

        var kind = _selected.GetValueOrDefault(args.Actor, ExchangeAccountKind.Personal);
        string? confirmation = null;
        var notice = args.Type switch
        {
            ExchangeOrderType.Market => TryOrder(ent, args.Actor, kind, args.Side, args.Company, args.Shares, out confirmation),
            ExchangeOrderType.Limit when args.Side is ExchangeOrderSide.Buy or ExchangeOrderSide.Sell =>
                TryPlaceLimit(ent, args.Actor, kind, args.Company, args.Side == ExchangeOrderSide.Buy, args.Shares, args.Limit,
                    out confirmation),
            _ => Loc.GetString("guild-exchange-bad-order"),
        };

        if (notice == null)
            RefreshAll();

        SendAccount(ent, args.Actor, notice, confirmation);
    }

    [SubscribeLocalEvent]
    private void OnCancelOrder(Entity<GuildExchangeComponent> ent, ref GuildExchangeCancelOrderMessage args)
    {
        var kind = _selected.GetValueOrDefault(args.Actor, ExchangeAccountKind.Personal);
        if (AccountFor(ent, args.Actor, kind, out _) is not { } account || !ExchangeLedger.CancelLimit(account, args.Id))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):user} cancelled limit order {args.Id} on the {AccountKind(account)} exchange account at {ToPrettyString(ent):machine}");

        if (account.Cargo)
            RefreshCargoViewers(account);
        else
            SendAccount(ent, args.Actor, null);
    }

    [SubscribeLocalEvent]
    private void OnFullBudget(Entity<GuildExchangeComponent> ent, ref GuildExchangeFullBudgetMessage args)
    {
        if (Throttled(ent, args.Actor, args.GetType()))
            _fullBudgetDue[args.Actor] = args.Enabled;
        else
            SendAccount(ent, args.Actor, SetFullBudget(ent, args.Actor, args.Enabled));
    }

    [SubscribeLocalEvent]
    private void OnWithdraw(Entity<GuildExchangeComponent> ent, ref GuildExchangeWithdrawMessage args)
    {
        if (!Throttled(ent, args.Actor, args.GetType(), answerLater: false))
            SendAccount(ent, args.Actor, Withdraw(ent, args.Actor));
    }

    private bool HasFullBudgetAccess(Entity<GuildExchangeComponent> ent, EntityUid actor)
    {
        return _access.FindAccessTags(actor).Contains(ent.Comp.FullBudgetAccess);
    }

    /// <summary>
    /// Unlocks the station's whole cargo budget for the actor, or locks it again. Returns why it failed, or null.
    /// </summary>
    private string? SetFullBudget(Entity<GuildExchangeComponent> ent, EntityUid actor, bool enabled)
    {
        if (GetCargoAccount(ent, out _) is not { } cargo)
            return Loc.GetString("guild-exchange-no-station");

        if (!enabled)
        {
            if (cargo.FullBudgetBy == actor)
                Relock(ent, cargo, actor);
            return null;
        }

        if (!HasFullBudgetAccess(ent, actor))
            return Loc.GetString("guild-exchange-full-budget-denied");

        if (cargo.FullBudgetBy == actor)
            return null;

        cargo.FullBudgetBy = actor;
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):user} unlocked the whole cargo budget of {cargo.Holder} for trading at {ToPrettyString(ent):machine}");
        return null;
    }

    private void Relock(Entity<GuildExchangeComponent> ent, ExchangeAccount cargo, EntityUid actor)
    {
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Whole-budget trading for {cargo.Holder} locked again at {ToPrettyString(ent):machine} ({ToPrettyString(actor):user})");
        cargo.FullBudgetBy = null;
        RefreshCargoViewers(cargo);
    }

    /// <summary>
    /// Sends the account to everyone looking at this cargo account, on any terminal.
    /// </summary>
    private void RefreshCargoViewers(ExchangeAccount cargo)
    {
        var query = EntityQueryEnumerator<GuildExchangeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var actor in _ui.GetActors(uid, GuildExchangeUiKey.Key))
            {
                if (_selected.GetValueOrDefault(actor) == ExchangeAccountKind.Cargo && GetCargoAccount(uid, out _) == cargo)
                    SendAccount((uid, comp), actor, null);
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<GuildExchangeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != ent.Comp.CashStack
            || stack.Count <= 0 || !_power.IsPowered(ent.Owner))
        {
            return;
        }

        args.Handled = true;
        if (GetPersonalAccount(args.User, out _) is not { } account)
        {
            _popup.PopupEntity(Loc.GetString("guild-exchange-no-id"), ent, args.User);
            return;
        }

        var amount = stack.Count;
        _stack.SetCount((args.Used, stack), 0);
        account.Cash += amount;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):user} deposited {amount} spesos into their Guild Exchange account at {ToPrettyString(ent):machine}");
        _popup.PopupEntity(Loc.GetString("guild-exchange-deposited", ("amount", amount)), ent, args.User);
        SendAccount(ent, args.User, null);
    }

    /// <summary>
    /// The ID a trader carries: the active hand first, then any other held item, then the ID slot.
    /// </summary>
    private bool TryFindTraderId(EntityUid user, out Entity<IdCardComponent> id)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (_idCard.TryGetIdCard(held, out id))
                return true;
        }

        return _idCard.TryFindIdCard(user, out id);
    }

    /// <summary>
    /// The personal account of whoever carries this ID card, made on first use.
    /// </summary>
    private ExchangeAccount? GetPersonalAccount(EntityUid user, out EntityUid owner)
    {
        owner = default;
        if (!TryFindTraderId(user, out var id))
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
    private ExchangeAccount? GetCargoAccount(EntityUid machine, out EntityUid station)
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
        owner = default;
        if (kind == ExchangeAccountKind.Personal)
            return GetPersonalAccount(actor, out owner);

        return _access.IsAllowed(actor, ent) ? GetCargoAccount(ent, out owner) : null;
    }

    /// <summary>
    /// A market order: buy, sell, short or cover. Returns why it failed, or null and what happened to the trader's money.
    /// </summary>
    private string? TryOrder(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, ExchangeOrderSide side,
        string companyId, int shares, out string? confirmation)
    {
        confirmation = null;
        if (_market is not { } market)
            return Loc.GetString("guild-exchange-closed");

        if (CheckAccount(ent, actor, kind, out var owner, out var fullBudget) is { } error)
            return error;

        var account = _accounts[owner];
        var wallet = WalletOf(owner, account);
        var before = wallet.Balance;
        long? cap = account.Cargo && !fullBudget ? ExchangeLedger.CapLeft(account, before, _settings.CargoCap) : null;

        var result = side switch
        {
            ExchangeOrderSide.Buy => ExchangeLedger.Buy(market, account, wallet, companyId, shares, cap),
            ExchangeOrderSide.Sell => ExchangeLedger.Sell(market, account, wallet, companyId, shares),
            ExchangeOrderSide.Short => ExchangeLedger.Short(market, account, wallet, companyId, shares, cap),
            ExchangeOrderSide.Cover => ExchangeLedger.Cover(market, account, wallet, companyId, shares),
            _ => ExchangeTradeResult.Fail("guild-exchange-bad-order"),
        };

        if (!result.Ok)
            return Loc.GetString(result.Error!);

        account.NextTrade = _timing.CurTime + _settings.TradeCooldown;
        var ticker = market.Companies[companyId].Proto.Ticker;
        LogTrade(ent, actor, account, side, ticker, result, fullBudget ? "whole budget unlocked" : null);

        // Shorts and covers move margin and collateral, not the trade's value, so say what the wallet did.
        var change = wallet.Balance - before;
        var key = side == ExchangeOrderSide.Cover && change < 0
            ? "guild-exchange-notice-covered-paid"
            : $"guild-exchange-notice-{Verb(side)}";
        confirmation = Loc.GetString(key, ("shares", result.Shares), ("ticker", ticker), ("money", Math.Abs(change)),
            ("price", Price(result)));
        return null;
    }

    /// <summary>
    /// Leaves an order to buy or sell when the price reaches <paramref name="limit"/>.
    /// </summary>
    private string? TryPlaceLimit(Entity<GuildExchangeComponent> ent, EntityUid actor, ExchangeAccountKind kind, string companyId,
        bool buy, int shares, float limit, out string? confirmation)
    {
        confirmation = null;
        if (_market is not { } market)
            return Loc.GetString("guild-exchange-closed");

        if (CheckAccount(ent, actor, kind, out var owner, out _) is { } error)
            return error;

        var account = _accounts[owner];
        var result = ExchangeLedger.PlaceLimit(market, account, companyId, buy, shares, limit, actor);
        if (!result.Ok)
            return Loc.GetString(result.Error!);

        account.NextTrade = _timing.CurTime + _settings.TradeCooldown;
        var ticker = market.Companies[companyId].Proto.Ticker;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(actor):user} placed a limit order to {(buy ? "buy" : "sell")} {shares} {ticker} at {limit:0.00} on the {AccountKind(account)} account at {ToPrettyString(ent):machine}");
        confirmation = Loc.GetString(buy ? "guild-exchange-notice-limit-buy" : "guild-exchange-notice-limit-sell",
            ("shares", shares), ("ticker", ticker), ("limit", limit.ToString("0.00")));
        return null;
    }

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
        if (account.Cargo && account.FullBudgetBy is { } unlockedBy)
        {
            if (unlockedBy == actor && HasFullBudgetAccess(ent, actor))
                fullBudget = true;
            else
                Relock(ent, account, actor);
        }

        return null;
    }

    private static string Verb(ExchangeOrderSide side)
    {
        return side switch
        {
            ExchangeOrderSide.Buy => "bought",
            ExchangeOrderSide.Sell => "sold",
            ExchangeOrderSide.Short => "shorted",
            _ => "covered",
        };
    }

    /// <summary>
    /// What one share went for, fee included.
    /// </summary>
    private static string Price(ExchangeTradeResult result)
    {
        return (result.Money / (float) Math.Max(1, result.Shares)).ToString("0.00");
    }

    private static string AccountKind(ExchangeAccount account)
    {
        return account.Cargo ? "cargo" : "personal";
    }

    private string TraderName(ExchangeAccount account)
    {
        return account.Cargo ? Loc.GetString("guild-exchange-cargo-trader") : account.Holder;
    }

    private void LogTrade(EntityUid machine, EntityUid actor, ExchangeAccount account, ExchangeOrderSide side, string ticker,
        ExchangeTradeResult result, string? note)
    {
        AddTradeLog(Loc.GetString($"guild-exchange-log-{Verb(side)}", ("trader", TraderName(account)), ("shares", result.Shares),
            ("ticker", ticker), ("money", result.Money), ("price", Price(result))));
        _adminLog.Add(LogType.Action, note != null ? LogImpact.Medium : LogImpact.Low,
            $"{ToPrettyString(actor):user} {side.ToString().ToLowerInvariant()} {result.Shares} {ticker} for {result.Money} spesos on the {AccountKind(account)} account{(note != null ? $" ({note})" : "")} at {ToPrettyString(machine):machine}");
    }

    private void AddTradeLog(string text)
    {
        _tradeLog.Insert(0, new ExchangeTradeLogState(_timing.CurTime, text));
        if (_tradeLog.Count > _settings.TradeLogKept)
            _tradeLog.RemoveAt(_tradeLog.Count - 1);
    }

    /// <summary>
    /// Borrowing fees, margin calls and limit orders, once a tick.
    /// </summary>
    private void TickAccounts()
    {
        if (_market is not { } market)
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
                AddTradeLog(Loc.GetString("guild-exchange-log-margin-call", ("trader", TraderName(account)), ("shares", result.Shares),
                    ("ticker", ticker), ("price", Price(result)), ("time", _time.GetStationTime().Time.ToString(@"hh\:mm"))));
                _adminLog.Add(LogType.Action, LogImpact.Medium,
                    $"Guild Exchange margin call covered {result.Shares} {ticker} for {result.Money} spesos on {account.Holder}'s {AccountKind(account)} account; {account.WrittenOff} spesos written off so far");
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
    private string? Withdraw(Entity<GuildExchangeComponent> ent, EntityUid actor)
    {
        if (GetPersonalAccount(actor, out _) is not { } account)
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

    private void SendAccount(Entity<GuildExchangeComponent> ent, EntityUid actor, string? notice, string? confirmation = null)
    {
        EntityUid station = default;
        var cargo = _access.IsAllowed(actor, ent) ? GetCargoAccount(ent, out station) : null;
        var kind = cargo == null ? ExchangeAccountKind.Personal : _selected.GetValueOrDefault(actor, ExchangeAccountKind.Personal);
        var personal = GetPersonalAccount(actor, out _);
        var fullBudget = cargo != null && cargo.FullBudgetBy == actor;
        var message = new GuildExchangeAccountMessage
        {
            Kind = kind,
            HasId = personal != null,
            CanTradeCargo = cargo != null,
            Notice = notice,
            Confirmation = confirmation,
            CanUnlockFullBudget = cargo != null && HasFullBudgetAccess(ent, actor),
            FullBudget = fullBudget,
        };

        var account = personal;
        message.Cash = personal?.Cash ?? 0;
        if (kind == ExchangeAccountKind.Cargo && cargo != null)
        {
            account = cargo;
            message.Cash = _cargo.GetBalanceFromAccount(station, cargo.CargoAccount);
            message.CapLeft = fullBudget ? null : ExchangeLedger.CapLeft(cargo, message.Cash, _settings.CargoCap);
        }

        if (account != null && _market is { } market)
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
    /// The closing bell when the round ends: shorts are covered, shares sold and cargo's money goes back into its
    /// budget. Personal cash nobody collected is lost.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New != GameRunLevel.PostRound || _market is not { } market || _accounts.Count == 0)
            return;

        _closingLines.Add(Loc.GetString("guild-exchange-round-end-bell", ("time", _time.GetStationTime().Time.ToString(@"hh\:mm"))));
        foreach (var (owner, account) in _accounts)
        {
            if (account.Cargo && !HasComp<StationBankAccountComponent>(owner))
                continue;

            ExchangeLedger.Settle(market, account, WalletOf(owner, account));
            if (account.Cargo && account.Traded)
            {
                _closingLines.Add(Loc.GetString("guild-exchange-round-end-cargo", ("station", account.Holder),
                    ("profit", account.Realised)));
            }
        }

        var best = _accounts.Values.Where(a => !a.Cargo && a.Traded).MaxBy(a => a.Realised);
        if (best != null)
            _closingLines.Add(Loc.GetString("guild-exchange-round-end-best", ("name", best.Holder), ("profit", best.Realised)));

        // The bell has rung; nothing trades until the next round.
        _market = null;
    }

    [SubscribeLocalEvent]
    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        foreach (var line in _closingLines)
        {
            args.AddLine(line);
        }
    }
}
