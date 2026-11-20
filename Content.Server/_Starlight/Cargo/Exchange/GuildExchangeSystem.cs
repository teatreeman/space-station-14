using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
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
/// Runs the round's market on the Guild Exchange: ticks prices, puts news on the wire, reacts to announced station
/// events and cargo orders, and keeps every open terminal up to date. Trading is in the other half of this system.
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
    private TimeSpan _nextTick;
    private TimeSpan _lastTick;
    private readonly List<ExchangeNewsState> _newsWire = new();
    private readonly List<CargoOrderData> _ordersToCheck = new();

    /// <summary>
    /// This round's market, made when the round starts.
    /// </summary>
    public ExchangeMarket? Market { get; private set; }

    public ExchangeSettingsPrototype Settings => _settings;

    /// <summary>
    /// The news wire, newest first.
    /// </summary>
    public IReadOnlyList<ExchangeNewsState> NewsWire => _newsWire;

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
        StartMarket(_random.Next());
    }

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        Market = null;
        _newsWire.Clear();
        _ordersToCheck.Clear();
        ResetAccounts();
    }

    /// <summary>
    /// Opens a fresh market from a seed, throwing away any old one.
    /// </summary>
    public void StartMarket(int seed)
    {
        Market = new ExchangeMarket(_proto, _settings, seed);
        _newsWire.Clear();
        _ordersToCheck.Clear();
        ResetAccounts();
        _lastTick = _timing.CurTime;
        _nextTick = _timing.CurTime + _settings.TickInterval;
    }

    /// <summary>
    /// The game time a market tick happens (or happened) at, counted from the latest one.
    /// </summary>
    public TimeSpan TickTime(int tick)
    {
        return _lastTick + _settings.TickInterval * (tick - (Market?.Ticks ?? 0));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Market == null)
            return;

        if (_ordersToCheck.Count > 0)
            CheckCargoOrders();

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick += _settings.TickInterval;
        Tick();
    }

    /// <summary>
    /// Moves the market on by one tick, then settles what that means for every account.
    /// </summary>
    public void Tick()
    {
        if (Market is not { } market)
            return;

        market.Tick();
        _lastTick = _timing.CurTime;
        TickAccounts();
        PostMarketNews();
        RefreshAll();
    }

    [SubscribeLocalEvent]
    private void OnGameRuleStarted(ref GameRuleStartedEvent args)
    {
        if (Market == null)
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
    public void ApplyShock(ExchangeShockPrototype shock)
    {
        if (Market is not { } market)
            return;

        market.ApplyShock(shock);
        PostMarketNews();
        RefreshAll();
    }

    /// <summary>
    /// Orders are offered to other systems before cargo charges for them, and can still fail after that, so they are
    /// only counted once cargo has approved and paid.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnFulfillCargoOrder(ref FulfillCargoOrderEvent args)
    {
        if (Market != null)
            _ordersToCheck.Add(args.Order);
    }

    private void CheckCargoOrders()
    {
        foreach (var order in _ordersToCheck)
        {
            if (order.Approved)
                RecordCargoOrder(order.Product, order.Price * order.OrderQuantity);
        }

        _ordersToCheck.Clear();
        PostMarketNews();
    }

    /// <summary>
    /// The company that supplies a cargo product.
    /// </summary>
    public string SupplierOf(ProtoId<CargoProductPrototype> product)
    {
        var suppliers = _proto.Index(SuppliersId);
        if (suppliers.Products.TryGetValue(product, out var company))
            return company;

        if (_proto.Resolve(product, out var proto) && suppliers.Categories.TryGetValue(proto.Category, out company))
            return company;

        return suppliers.Default;
    }

    /// <summary>
    /// A paid cargo order: its supplier's order book fills a little.
    /// </summary>
    public void RecordCargoOrder(ProtoId<CargoProductPrototype> product, int spend)
    {
        Market?.RecordOrder(SupplierOf(product), spend);
    }

    /// <summary>
    /// Puts what the market said since last time on the wire, and pays any dividends it declared.
    /// </summary>
    private void PostMarketNews()
    {
        if (Market is not { } market)
            return;

        foreach (var item in market.TakeNews())
        {
            if (item.Kind == ExchangeNewsKind.Dividend && item.Company != null)
                PayDividend(item.Company, item.Amount);

            PostNews(TickerFor(item), NewsText(item), item.Kind == ExchangeNewsKind.Rumour);
        }
    }

    private string? TickerFor(ExchangeNewsItem item)
    {
        if (Market is not { } market)
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
        var name = Market != null && item.Company != null && Market.Companies.TryGetValue(item.Company, out var company)
            ? Loc.GetString(company.Proto.Name)
            : string.Empty;

        return Loc.GetString(item.Text,
            ("company", name),
            ("actual", item.Actual.ToString("0.00")),
            ("consensus", item.Consensus.ToString("0.00")),
            ("amount", item.Amount.ToString("0.00")));
    }

    private void PostNews(string? ticker, string text, bool unconfirmed)
    {
        _newsWire.Insert(0, new ExchangeNewsState(_timing.CurTime, ticker, text, unconfirmed));
        if (_newsWire.Count > _settings.NewsKept)
            _newsWire.RemoveAt(_newsWire.Count - 1);
    }

    /// <summary>
    /// A time as players see it everywhere on the exchange: station time.
    /// </summary>
    public string StationTime(TimeSpan curTime)
    {
        return ExchangeTime.Format(_time, curTime);
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

        if (Market is not { } market)
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
                Description = Loc.GetString(proto.Description),
                Sector = Loc.GetString(proto.Sector),
                Price = company.Price,
                Open = proto.Price,
                Bid = ExchangeMath.Bid(company.Price, proto.Spread),
                Ask = ExchangeMath.Ask(company.Price, proto.Spread),
                Spread = proto.Spread,
                Volume = candles.Count > 0 ? candles[^1].Volume : 0f,
                Spark = candles.Skip(Math.Max(0, candles.Count - _settings.SparkLength)).Select(c => c.Close).ToList(),
            });
        }

        foreach (var report in market.Earnings)
        {
            state.Calendar.Add(new ExchangeCalendarState(market.Companies[report.Company].Proto.Ticker, TickTime(report.Tick),
                false, report.Consensus, report.Actual, 0f));
        }

        foreach (var company in market.Companies.Values)
        {
            if (company.Proto.Dividend > 0f)
            {
                state.Calendar.Add(new ExchangeCalendarState(company.Proto.Ticker, TickTime(market.NextDividendTick), true, 0f,
                    null, company.Price * company.Proto.Dividend));
            }
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

    private void SendChart(Entity<GuildExchangeComponent> ent, EntityUid actor)
    {
        if (Market is not { } market || !_watching.TryGetValue(actor, out var companyId)
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
