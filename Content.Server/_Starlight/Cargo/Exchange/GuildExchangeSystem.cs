using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Cargo.Exchange;

/// <summary>
/// Runs the round's market on the Guild Exchange: ticks prices, posts news, reacts to announced station events and
/// settles cargo's shares at round end. Trading is in the other half of this system.
/// </summary>
public sealed partial class GuildExchangeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<ExchangeSettingsPrototype> SettingsId = "Default";

    private ExchangeSettingsPrototype _settings = default!;
    private TimeSpan _nextTick;
    private TimeSpan _nextSectorNews;
    private readonly List<ExchangeNewsState> _newsWire = new();
    private readonly List<(int Tick, string Company)> _probes = new();

    /// <summary>
    /// This round's market, made when the round starts.
    /// </summary>
    public ExchangeMarket? Market { get; private set; }

    public ExchangeSettingsPrototype Settings => _settings;

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
        _probes.Clear();
        ResetAccounts();
    }

    /// <summary>
    /// Opens a fresh market from a seed, throwing away any old one.
    /// </summary>
    public void StartMarket(int seed)
    {
        Market = new ExchangeMarket(_proto.EnumeratePrototypes<ExchangeCompanyPrototype>(), seed);
        _newsWire.Clear();
        _probes.Clear();
        ResetAccounts();
        _nextTick = _timing.CurTime + _settings.TickInterval;
        _nextSectorNews = _timing.CurTime + NextSectorNewsDelay();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Market == null || _timing.CurTime < _nextTick)
            return;

        _nextTick += _settings.TickInterval;
        Tick();
    }

    /// <summary>
    /// Moves the market on by one tick: prices, investigation verdicts and sector news.
    /// </summary>
    public void Tick()
    {
        if (Market is not { } market)
            return;

        market.Tick(_settings);

        for (var i = _probes.Count - 1; i >= 0; i--)
        {
            if (_probes[i].Tick > market.Ticks)
                continue;

            Verdict(_probes[i].Company);
            _probes.RemoveAt(i);
        }

        if (_timing.CurTime >= _nextSectorNews)
        {
            _nextSectorNews = _timing.CurTime + NextSectorNewsDelay();
            SectorNews();
        }

        RefreshAll();
    }

    private TimeSpan NextSectorNewsDelay()
    {
        var min = _settings.SectorNewsMin.TotalSeconds;
        var max = _settings.SectorNewsMax.TotalSeconds;
        return TimeSpan.FromSeconds(min + (max - min) * (Market?.NextFloat() ?? 0.5f));
    }

    [SubscribeLocalEvent]
    private void OnGameRuleStarted(ref GameRuleStartedEvent args)
    {
        if (Market == null)
            return;

        foreach (var shock in _proto.EnumeratePrototypes<ExchangeShockPrototype>())
        {
            if (!shock.Rules.Contains(args.RuleId))
                continue;

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

        var headline = Loc.GetString(shock.Headline);
        foreach (var (company, size) in shock.Shocks)
        {
            market.QueueShock(company, size);
        }

        var tickers = shock.Shocks.Keys
            .Select(c => market.Companies.TryGetValue(c, out var co) ? co.Proto.Ticker : null)
            .Where(t => t != null);
        PostNews(string.Join(" ", tickers), headline, false);
        RefreshAll();
    }

    /// <summary>
    /// A story about one company. Some are only rumours, and a quarter of those are false; some are investigations
    /// whose verdict comes a few ticks later.
    /// </summary>
    private void SectorNews()
    {
        if (Market is not { } market || market.Companies.Count == 0)
            return;

        var company = market.Companies.Values.ElementAt(market.Next(0, market.Companies.Count));
        var name = Loc.GetString(company.Proto.Name);

        if (market.NextFloat() < _settings.ProbeChance)
        {
            _probes.Add((market.Ticks + market.Next(_settings.ProbeMinTicks, _settings.ProbeMaxTicks + 1), company.Proto.ID));
            PostNews(company.Proto.Ticker, Loc.GetString("guild-exchange-news-probe", ("company", name)), false);
            return;
        }

        var good = market.NextFloat() < 0.5f;
        var stories = good ? company.Proto.GoodNews : company.Proto.BadNews;
        if (stories.Count == 0)
            return;

        var story = Loc.GetString(stories[market.Next(0, stories.Count)], ("company", name));
        var rumour = market.NextFloat() < _settings.RumourChance;
        PostNews(company.Proto.Ticker, story, rumour);

        if (rumour && market.NextFloat() < _settings.FalseRumourChance)
            return;

        market.QueueShock(company.Proto.ID, good ? company.Proto.NewsShock : -company.Proto.NewsShock);
    }

    private void Verdict(string companyId)
    {
        if (Market is not { } market || !market.Companies.TryGetValue(companyId, out var company))
            return;

        var cleared = market.NextFloat() < 0.5f;
        var name = Loc.GetString(company.Proto.Name);
        PostNews(company.Proto.Ticker,
            Loc.GetString(cleared ? "guild-exchange-news-probe-cleared" : "guild-exchange-news-probe-guilty", ("company", name)),
            false);
        market.QueueShock(companyId, cleared ? company.Proto.NewsShock : -2f * company.Proto.NewsShock);
    }

    private void PostNews(string? ticker, string text, bool unconfirmed)
    {
        _newsWire.Insert(0, new ExchangeNewsState(_ticker.RoundDuration(), ticker, text, unconfirmed));
        if (_newsWire.Count > _settings.NewsKept)
            _newsWire.RemoveAt(_newsWire.Count - 1);
    }

    /// <summary>
    /// The news wire, newest first.
    /// </summary>
    public IReadOnlyList<ExchangeNewsState> NewsWire => _newsWire;

    private GuildExchangeBuiState BuildState()
    {
        var companies = new List<ExchangeCompanyState>();
        if (Market is { } market)
        {
            foreach (var company in market.Companies.Values)
            {
                companies.Add(new ExchangeCompanyState(company.Proto.ID,
                    company.Proto.Ticker,
                    Loc.GetString(company.Proto.Name),
                    Loc.GetString(company.Proto.Description),
                    Loc.GetString(company.Proto.Sector),
                    company.Price,
                    company.Proto.Price,
                    company.Proto.Liquidity,
                    new List<float>(company.History)));
            }
        }

        return new GuildExchangeBuiState(companies, new List<ExchangeNewsState>(_newsWire), new List<string>(_tradeLog),
            _settings.Fee, _settings.Spread);
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
            }
        }
    }
}
