using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking.Events;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared._Starlight.Time;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Cargo.Exchange;

/// <summary>
/// Runs the Guild Exchange market: price ticks, the news wire, station events and cargo orders.
/// </summary>
public sealed partial class GuildExchangeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedTimeSystem _time = default!;

    private static readonly ProtoId<ExchangeSettingsPrototype> SettingsId = "Default";
    private static readonly ProtoId<ExchangeSuppliersPrototype> SuppliersId = "Default";

    private ExchangeSettingsPrototype _settings = default!;
    private ExchangeMarket? _market;
    private TimeSpan _nextTick;
    private TimeSpan _lastTick;
    private readonly List<ExchangeNewsState> _newsWire = new();
    private readonly List<CargoOrderData> _ordersToCheck = new();

    public override void Initialize()
    {
        base.Initialize();
        _settings = _proto.Index(SettingsId);
        _proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _proto.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ExchangeSettingsPrototype>())
            _settings = _proto.Index(SettingsId);
    }

    [SubscribeLocalEvent]
    private void OnRoundStarting(RoundStartingEvent args)
    {
        StartMarket();
    }

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _market = null;
        _newsWire.Clear();
        _ordersToCheck.Clear();
        ResetAccounts();
    }

    private void StartMarket()
    {
        _market = new ExchangeMarket(_proto, _settings, _random);
        _newsWire.Clear();
        _ordersToCheck.Clear();
        ResetAccounts();
        _lastTick = _timing.CurTime;
        _nextTick = _timing.CurTime + _settings.TickInterval;
    }

    private TimeSpan TickTime(int tick)
    {
        return _lastTick + _settings.TickInterval * (tick - (_market?.Ticks ?? 0));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_market == null)
            return;

        if (_ordersToCheck.Count > 0)
            CheckCargoOrders();

        if (_replyDue.Count > 0)
            SendDueReplies();

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick += _settings.TickInterval;
        Tick();
    }

    private void Tick()
    {
        if (_market is not { } market)
            return;

        market.Tick();
        _lastTick = _timing.CurTime;

        // Dividends go to whoever held on the ex-dividend date, before this tick's limit fills and margin calls.
        var news = market.TakeNews();
        foreach (var item in news)
        {
            if (item is { Kind: ExchangeNewsKind.Dividend, Company: { } company })
                PayDividend(company, item.Amount);
        }

        TickAccounts();
        PostNews(news);
        RefreshAll();
    }

    [SubscribeLocalEvent]
    private void OnGameRuleStarted(ref GameRuleStartedEvent args)
    {
        if (_market == null)
            return;

        foreach (var shock in _proto.EnumeratePrototypes<ExchangeShockPrototype>())
        {
            if (shock.Rules.Contains(args.RuleId))
                ApplyShock(shock);
        }
    }

    /// <summary>
    /// Posts a shock's headline now and moves the prices on the next tick.
    /// </summary>
    private void ApplyShock(ExchangeShockPrototype shock)
    {
        if (_market is not { } market)
            return;

        market.ApplyShock(shock);
        PostNews(market.TakeNews());
        RefreshState();
    }

    /// <summary>
    /// Cargo charges for an order after raising this, so it only counts once it shows up as approved.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnFulfillCargoOrder(ref FulfillCargoOrderEvent args)
    {
        if (_market != null)
            _ordersToCheck.Add(args.Order);
    }

    private void CheckCargoOrders()
    {
        if (_market is not { } market)
            return;

        foreach (var order in _ordersToCheck)
        {
            if (order.Approved)
                market.RecordOrder(SupplierOf(order.Product), order.Price * order.OrderQuantity);
        }

        _ordersToCheck.Clear();
        var news = market.TakeNews();
        if (news.Count == 0)
            return;

        PostNews(news);
        RefreshState();
    }

    private string SupplierOf(ProtoId<CargoProductPrototype> product)
    {
        var suppliers = _proto.Index(SuppliersId);
        if (suppliers.Products.TryGetValue(product, out var company))
            return company;

        if (_proto.Resolve(product, out var proto) && suppliers.Categories.TryGetValue(proto.Category, out company))
            return company;

        return suppliers.Default;
    }

    private void PostNews(List<ExchangeNewsItem> news)
    {
        foreach (var item in news)
        {
            _newsWire.Insert(0, new ExchangeNewsState(_timing.CurTime, item.Kind, TickerFor(item), NewsText(item)));
        }

        if (_newsWire.Count > _settings.NewsKept)
            _newsWire.RemoveRange(_settings.NewsKept, _newsWire.Count - _settings.NewsKept);
    }

    private string? TickerFor(ExchangeNewsItem item)
    {
        if (_market is not { } market)
            return null;

        if (item.Company != null && market.Companies.TryGetValue(item.Company, out var company))
            return company.Proto.Ticker;

        if (item.Factor != null && _proto.Resolve<ExchangeFactorPrototype>(item.Factor, out var factor))
            return factor.Tag;

        if (item.Moves is { Count: > 0 } moves)
        {
            return string.Join(" ", moves.Keys
                .Select(c => market.Companies.TryGetValue(c, out var co) ? co.Proto.Ticker : null)
                .Where(t => t != null));
        }

        return null;
    }

    private string NewsText(ExchangeNewsItem item)
    {
        var ticker = _market != null && item.Company != null && _market.Companies.TryGetValue(item.Company, out var company)
            ? company.Proto.Ticker
            : string.Empty;

        return Loc.GetString(item.Text,
            ("company", ticker),
            ("actual", item.Actual.ToString("0.00")),
            ("consensus", item.Consensus.ToString("0.00")),
            ("amount", item.Amount.ToString("0.00")));
    }

    private GuildExchangeBuiState BuildState()
    {
        var state = new GuildExchangeBuiState
        {
            Trades = new List<ExchangeTradeLogState>(_tradeLog),
            News = new List<ExchangeNewsState>(_newsWire),
            Fee = _settings.Fee,
            BorrowFee = _settings.BorrowFee,
            ShortMargin = _settings.ShortMargin,
            MaintenanceMargin = _settings.MaintenanceMargin,
            MaxOrders = _settings.MaxOpenOrders,
        };

        if (_market is not { } market)
            return state;

        foreach (var company in market.Companies.Values)
        {
            var proto = company.Proto;
            var candles = company.Candles;
            state.Companies.Add(new ExchangeCompanyState
            {
                Id = proto.ID,
                Ticker = proto.Ticker,
                Name = Loc.GetString(proto.Name),
                Sector = Loc.GetString(proto.Sector),
                Price = company.Price,
                Open = proto.Price,
                Bid = ExchangeMath.Bid(company.Price, proto.Spread),
                Ask = ExchangeMath.Ask(company.Price, proto.Spread),
                Spread = proto.Spread,
                Volume = candles.Count > 0 ? candles[^1].Volume : 0f,
                Spark = candles.Skip(Math.Max(0, candles.Count - _settings.SparkLength)).Select(c => c.Close).ToList(),
            });

            if (proto.Dividend > 0f)
            {
                state.Calendar.Add(new ExchangeCalendarState(proto.Ticker, TickTime(market.NextDividendTick), true, 0f,
                    null, company.Price * proto.Dividend));
            }
        }

        foreach (var report in market.Earnings)
        {
            state.Calendar.Add(new ExchangeCalendarState(market.Companies[report.Company].Proto.Ticker, TickTime(report.Tick),
                false, report.Consensus, report.Actual, 0f));
        }

        state.Calendar.Sort((a, b) => a.Time.CompareTo(b.Time));
        return state;
    }

    private void RefreshAll()
    {
        var query = EntityQueryEnumerator<GuildExchangeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_ui.IsUiOpen(uid, GuildExchangeUiKey.Key))
                continue;

            _ui.SetUiState(uid, GuildExchangeUiKey.Key, BuildState());
            foreach (var actor in _ui.GetActors(uid, GuildExchangeUiKey.Key).ToList())
            {
                SendAccount((uid, comp), actor, null);
                SendChart((uid, comp), actor);
            }
        }
    }

    /// <summary>
    /// Puts the market's state on every open terminal, without the per-viewer account and chart.
    /// </summary>
    private void RefreshState()
    {
        var query = EntityQueryEnumerator<GuildExchangeComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, GuildExchangeUiKey.Key))
                _ui.SetUiState(uid, GuildExchangeUiKey.Key, BuildState());
        }
    }

    private void SendDueReplies()
    {
        foreach (var (actor, (machine, at)) in _replyDue.ToList())
        {
            if (_timing.CurTime < at)
                continue;

            _replyDue.Remove(actor);
            var budget = _fullBudgetDue.Remove(actor, out var enabled);
            if (!TryComp<GuildExchangeComponent>(machine, out var comp) || !_ui.IsUiOpen(machine, GuildExchangeUiKey.Key, actor))
                continue;

            var notice = budget ? SetFullBudget((machine, comp), actor, enabled) : null;
            SendAccount((machine, comp), actor, notice);
            SendChart((machine, comp), actor);
        }
    }

    private void SendChart(Entity<GuildExchangeComponent> ent, EntityUid actor)
    {
        if (_market is not { } market || !_watching.TryGetValue(actor, out var companyId)
            || !market.Companies.TryGetValue(companyId, out var company))
        {
            return;
        }

        _ui.ServerSendUiMessage(ent.Owner, GuildExchangeUiKey.Key,
            new GuildExchangeChartMessage(companyId, new List<ExchangeCandle>(company.Candles), TickTime(market.Ticks),
                _settings.TickInterval),
            actor);
    }
}
