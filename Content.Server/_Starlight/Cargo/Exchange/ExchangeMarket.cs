using System.Linq;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Cargo.Exchange;

public enum ExchangeNewsKind : byte
{
    Company,
    Rumour,
    Warning,
    Outcome,
    Factor,
    Event,
    Earnings,
    Dividend,
    Orders,
}

/// <summary>
/// Something the market put on the wire. <see cref="Text"/> is a locale id; <see cref="Sentiment"/> is what a careful
/// reader would take from it (+1 good, -1 bad, 0 unclear). <see cref="Moves"/> lists the value moves an event headline
/// sets off, by company, which players learn from experience.
/// </summary>
public sealed record ExchangeNewsItem(
    int Tick,
    ExchangeNewsKind Kind,
    string Text,
    int Sentiment,
    string? Company = null,
    string? Factor = null,
    float Actual = 0f,
    float Consensus = 0f,
    float Amount = 0f,
    IReadOnlyDictionary<string, float>? Moves = null,
    string? Story = null);

/// <summary>
/// A scheduled earnings report. The consensus is public from the start of the round; the actual result is made when
/// the report comes out, from what happened to the company since its last one.
/// </summary>
public sealed class ExchangeEarningsReport(string company, int tick, float consensus)
{
    public readonly string Company = company;
    public readonly int Tick = tick;
    public readonly float Consensus = consensus;
    public float? Actual;
}

/// <summary>
/// One round's market. Each company has a hidden value that news, events and orders move and that drifts back to its
/// opening price, and a visible price that closes part of the gap each tick while sector factors and its own noise push
/// it around. Everything random comes from one seed, so a market can be replayed, and nothing here touches entities, so
/// whole rounds can be simulated in tests.
/// </summary>
public sealed class ExchangeMarket
{
    public sealed class Company(ExchangeCompanyPrototype proto)
    {
        public readonly ExchangeCompanyPrototype Proto = proto;
        public float Price = proto.Price;
        public float Value = proto.Price;

        /// <summary>
        /// Extra value from station orders, as a share of the value. Fades like any other news.
        /// </summary>
        public float OrderBoost;

        /// <summary>
        /// What happened to the company since its last report, as summed value moves. Feeds its next earnings.
        /// </summary>
        public float QuarterNews;
        public float QuarterOrders;

        public float LastReturn;
        public readonly List<ExchangeCandle> Candles = new();

        internal float TickVolume;
        internal float OrderAddedThisTick;
        internal int LastOrderHeadline = -100;
    }

    private sealed record PendingStory(int Tick, string Company, ExchangeStoryOutcome Outcome);

    private readonly Random _random;
    private readonly IPrototypeManager _proto;
    private readonly ExchangeSettingsPrototype _settings;
    private readonly List<(int Tick, string Company, float Shock)> _pending = new();
    private readonly List<(int Tick, string Factor, float Shock)> _pendingFactors = new();
    private readonly List<PendingStory> _stories = new();
    private readonly List<ExchangeFactorPrototype> _factors;
    private readonly List<ExchangeStoryPrototype> _storyProtos;
    private readonly float[] _factorMoves;
    private readonly List<ExchangeNewsItem> _news = new();
    private int _nextNews;

    /// <summary>
    /// Companies by prototype id, in id order so ticks are reproducible.
    /// </summary>
    public readonly SortedDictionary<string, Company> Companies = new();

    /// <summary>
    /// This round's earnings calendar, soonest first.
    /// </summary>
    public readonly List<ExchangeEarningsReport> Earnings = new();

    public int Ticks { get; private set; }

    public ExchangeSettingsPrototype Settings => _settings;

    public ExchangeMarket(IPrototypeManager proto, ExchangeSettingsPrototype settings, int seed)
    {
        _proto = proto;
        _settings = settings;
        _random = new Random(seed);
        foreach (var company in proto.EnumeratePrototypes<ExchangeCompanyPrototype>().OrderBy(c => c.ID))
        {
            Companies[company.ID] = new Company(company);
        }

        _factors = proto.EnumeratePrototypes<ExchangeFactorPrototype>().OrderBy(f => f.ID).ToList();
        _storyProtos = proto.EnumeratePrototypes<ExchangeStoryPrototype>().OrderBy(s => s.ID).ToList();
        _factorMoves = new float[_factors.Count];
        _nextNews = NextNewsDelay();
        ScheduleEarnings();
    }

    /// <summary>
    /// The tick of the next dividend date.
    /// </summary>
    public int NextDividendTick => (Ticks / _settings.DividendEvery + 1) * _settings.DividendEvery;

    /// <summary>
    /// Hands over the news put out since last asked, oldest first.
    /// </summary>
    public List<ExchangeNewsItem> TakeNews()
    {
        var news = new List<ExchangeNewsItem>(_news);
        _news.Clear();
        return news;
    }

    /// <summary>
    /// Moves a company's value by <paramref name="shock"/> (a share of it) at the start of a later tick.
    /// </summary>
    public void QueueShock(string company, float shock, int inTicks = 1)
    {
        _pending.Add((Ticks + inTicks, company, shock));
    }

    public void QueueFactorShock(string factor, float shock, int inTicks = 1)
    {
        _pendingFactors.Add((Ticks + inTicks, factor, shock));
    }

    /// <summary>
    /// Queues an announced station event's moves and puts its headline on the wire.
    /// </summary>
    public void ApplyShock(ExchangeShockPrototype shock)
    {
        var moves = new Dictionary<string, float>();
        foreach (var (company, size) in shock.Shocks)
        {
            if (!Companies.ContainsKey(company))
                continue;
            QueueShock(company, size);
            moves[company] = size;
        }

        foreach (var (factor, size) in shock.Factors)
        {
            QueueFactorShock(factor, size);
            foreach (var company in Companies.Values)
            {
                if (company.Proto.Factors.TryGetValue(factor, out var loading))
                    moves[company.Proto.ID] = moves.GetValueOrDefault(company.Proto.ID) + size * loading;
            }
        }

        _news.Add(new ExchangeNewsItem(Ticks, ExchangeNewsKind.Event, shock.Headline, 0, Moves: moves));
    }

    /// <summary>
    /// A station bought <paramref name="spend"/> spesos of the company's goods. Lifts its order premium a little,
    /// with diminishing returns and a cap per tick, and counts toward its next earnings.
    /// </summary>
    public void RecordOrder(string companyId, int spend)
    {
        if (spend <= 0 || !Companies.TryGetValue(companyId, out var company))
            return;

        var room = MathF.Max(0f, _settings.OrderTickCap - company.OrderAddedThisTick);
        var add = MathF.Min(room, spend / _settings.OrderScale * MathF.Max(0f, _settings.OrderMaxBoost - company.OrderBoost));
        company.OrderBoost += add;
        company.OrderAddedThisTick += add;
        company.QuarterOrders += add;

        if (company.OrderBoost >= _settings.OrderHeadlineAt && Ticks - company.LastOrderHeadline >= 10)
        {
            company.LastOrderHeadline = Ticks;
            _news.Add(new ExchangeNewsItem(Ticks, ExchangeNewsKind.Orders, Pick(_settings.OrderNews), 1, companyId));
        }
    }

    /// <summary>
    /// Players' trades fill at the quoted price and don't move it, but they do show in the volume.
    /// </summary>
    public void AddVolume(Company company, int shares)
    {
        company.TickVolume += Math.Abs(shares);
    }

    public void Tick()
    {
        Ticks++;
        ApplyDueShocks();

        for (var i = 0; i < _factors.Count; i++)
        {
            _factorMoves[i] = _factors[i].Volatility * Gaussian();
        }

        foreach (var company in Companies.Values)
        {
            TickCompany(company);
        }

        if (Ticks % _settings.DividendEvery == 0)
            PayDividends();

        foreach (var report in Earnings)
        {
            if (report.Tick == Ticks && report.Actual == null)
                Report(report);
        }

        for (var i = _stories.Count - 1; i >= 0; i--)
        {
            if (_stories[i].Tick > Ticks)
                continue;

            ResolveStory(_stories[i]);
            _stories.RemoveAt(i);
        }

        if (Ticks >= _nextNews)
        {
            _nextNews = Ticks + NextNewsDelay();
            News();
        }
    }

    private void ApplyDueShocks()
    {
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var (due, id, shock) = _pending[i];
            if (due > Ticks)
                continue;

            if (Companies.TryGetValue(id, out var company))
            {
                company.Value = MathF.Max(ExchangeMath.MinPrice, company.Value * (1f + shock));
                company.QuarterNews += shock;
            }

            _pending.RemoveAt(i);
        }

        for (var i = _pendingFactors.Count - 1; i >= 0; i--)
        {
            var (due, id, shock) = _pendingFactors[i];
            if (due > Ticks)
                continue;

            foreach (var company in Companies.Values)
            {
                if (!company.Proto.Factors.TryGetValue(id, out var loading))
                    continue;
                company.Value = MathF.Max(ExchangeMath.MinPrice, company.Value * (1f + shock * loading));
                company.QuarterNews += shock * loading;
            }

            _pendingFactors.RemoveAt(i);
        }
    }

    private void TickCompany(Company company)
    {
        var proto = company.Proto;
        company.Value += _settings.ValueReversion * (proto.Price - company.Value);
        company.OrderBoost *= 1f - _settings.ValueReversion;
        company.OrderAddedThisTick = 0f;

        var noise = proto.Volatility * Gaussian();
        for (var i = 0; i < _factors.Count; i++)
        {
            if (proto.Factors.TryGetValue(_factors[i].ID, out var loading))
                noise += loading * _factorMoves[i];
        }

        var target = company.Value * (1f + company.OrderBoost);
        var open = company.Price;
        var change = proto.PriceReversion * (target - open) / open + proto.Momentum * company.LastReturn + noise;
        var close = MathF.Max(ExchangeMath.MinPrice, open * (1f + change));
        company.LastReturn = close / open - 1f;
        company.Price = close;

        var wick = 1f + MathF.Abs(Gaussian()) * proto.Volatility * 0.5f;
        var high = MathF.Max(open, close) * wick;
        var low = MathF.Min(open, close) / wick;
        var volume = proto.Volume * (1f + 3f * MathF.Abs(company.LastReturn) / (proto.Volatility + 0.001f))
            * MathF.Exp(0.3f * Gaussian()) + company.TickVolume;

        company.Candles.Add(new ExchangeCandle(open, high, low, close, volume));
        if (company.Candles.Count > _settings.HistoryLength)
            company.Candles.RemoveAt(0);

        company.TickVolume = 0f;
    }

    /// <summary>
    /// Holders get the dividend; the price drops by the same amount, as on a real ex-dividend date.
    /// </summary>
    private void PayDividends()
    {
        foreach (var company in Companies.Values)
        {
            if (company.Proto.Dividend <= 0f)
                continue;

            var amount = company.Price * company.Proto.Dividend;
            company.Price = MathF.Max(ExchangeMath.MinPrice, company.Price - amount);
            company.Value = MathF.Max(ExchangeMath.MinPrice, company.Value - amount);
            _news.Add(new ExchangeNewsItem(Ticks, ExchangeNewsKind.Dividend, "guild-exchange-dividend", 0, company.Proto.ID,
                Amount: amount));
        }
    }

    private void ScheduleEarnings()
    {
        foreach (var company in Companies.Values)
        {
            var first = Next(_settings.EarningsMinTick, _settings.EarningsMaxTick + 1);
            Earnings.Add(new ExchangeEarningsReport(company.Proto.ID, first, Consensus(company)));

            var second = first + Next(40, 70);
            if (NextFloat() < _settings.SecondReportChance)
                Earnings.Add(new ExchangeEarningsReport(company.Proto.ID, second, Consensus(company)));
        }

        Earnings.Sort((a, b) => a.Tick.CompareTo(b.Tick));
    }

    private float Consensus(Company company)
    {
        return MathF.Round(company.Proto.Earnings * (1f + _settings.ConsensusNoise * Gaussian()), 2);
    }

    /// <summary>
    /// The result follows what happened to the company this quarter, plus some luck; the move follows the surprise.
    /// </summary>
    private void Report(ExchangeEarningsReport report)
    {
        var company = Companies[report.Company];
        var actual = company.Proto.Earnings * (1f
            + _settings.EarningsFromNews * company.QuarterNews
            + _settings.EarningsFromOrders * company.QuarterOrders
            + _settings.EarningsNoise * Gaussian());
        actual = MathF.Round(actual, 2);
        report.Actual = actual;
        company.QuarterNews = 0f;
        company.QuarterOrders = 0f;

        var surprise = actual / MathF.Max(0.01f, report.Consensus) - 1f;
        var shock = Math.Clamp(surprise * company.Proto.EarningsSensitivity, -_settings.EarningsMaxShock, _settings.EarningsMaxShock);
        QueueShock(company.Proto.ID, shock);

        var (text, sentiment) = MathF.Abs(surprise) < _settings.InLineBand ? ("guild-exchange-earnings-inline", 0)
            : surprise > 0 ? ("guild-exchange-earnings-beat", 1)
            : ("guild-exchange-earnings-miss", -1);
        _news.Add(new ExchangeNewsItem(Ticks, ExchangeNewsKind.Earnings, text, sentiment, company.Proto.ID,
            Actual: actual, Consensus: report.Consensus));
    }

    private void News()
    {
        var roll = NextFloat();
        if (roll < _settings.FactorNewsChance && _factors.Count > 0)
        {
            var factor = _factors[Next(0, _factors.Count)];
            var good = NextFloat() < 0.5f;
            AnnounceFactor(factor.ID, (good ? 1f : -1f) * factor.NewsShock * Size());
            _news.Add(new ExchangeNewsItem(Ticks, ExchangeNewsKind.Factor, Pick(good ? factor.GoodNews : factor.BadNews),
                good ? 1 : -1, Factor: factor.ID));
            return;
        }

        var company = Companies.Values.ElementAt(Next(0, Companies.Count));
        if (roll < _settings.FactorNewsChance + _settings.StoryChance && _storyProtos.Count > 0)
        {
            var story = _storyProtos[Next(0, _storyProtos.Count)];
            var outcome = PickOutcome(story);
            _stories.Add(new PendingStory(Ticks + Next(story.MinTicks, story.MaxTicks + 1), company.Proto.ID, outcome));
            _news.Add(new ExchangeNewsItem(Ticks, ExchangeNewsKind.Warning, story.Warning, 0, company.Proto.ID, Story: story.ID));
            return;
        }

        var positive = NextFloat() < 0.5f;
        var general = NextFloat() < _settings.GeneralNewsChance;
        var dataset = general
            ? positive ? _settings.GeneralGoodNews : _settings.GeneralBadNews
            : positive ? company.Proto.GoodNews : company.Proto.BadNews;
        var rumour = NextFloat() < _settings.RumourChance;
        var text = Pick(dataset);
        var shock = (positive ? 1f : -1f) * company.Proto.NewsShock * Size();

        _news.Add(new ExchangeNewsItem(Ticks, rumour ? ExchangeNewsKind.Rumour : ExchangeNewsKind.Company, text,
            positive ? 1 : -1, company.Proto.ID));

        if (rumour && NextFloat() < _settings.FalseRumourChance)
            return;

        Announce(company, shock);
    }

    /// <summary>
    /// A headline's move: some of it is in the price already, the rest lands next tick, and now and then the market
    /// shrugs it off and goes the other way.
    /// </summary>
    private void Announce(Company company, float shock)
    {
        var real = NextFloat() < _settings.NewsFadeChance ? -0.5f * shock : shock;
        var now = real * _settings.NewsPricedIn;
        company.Value = MathF.Max(ExchangeMath.MinPrice, company.Value * (1f + now));
        company.Price = MathF.Max(ExchangeMath.MinPrice, company.Price * (1f + now));
        company.QuarterNews += now;
        QueueShock(company.Proto.ID, real - now);
    }

    private void AnnounceFactor(string factor, float shock)
    {
        var real = NextFloat() < _settings.NewsFadeChance ? -0.5f * shock : shock;
        var now = real * _settings.NewsPricedIn;
        foreach (var company in Companies.Values)
        {
            if (!company.Proto.Factors.TryGetValue(factor, out var loading))
                continue;
            company.Value = MathF.Max(ExchangeMath.MinPrice, company.Value * (1f + now * loading));
            company.Price = MathF.Max(ExchangeMath.MinPrice, company.Price * (1f + now * loading));
            company.QuarterNews += now * loading;
        }

        QueueFactorShock(factor, real - now);
    }

    private void ResolveStory(PendingStory story)
    {
        if (story.Outcome.Headline is not { } headline || !Companies.TryGetValue(story.Company, out var company))
            return;

        var shock = story.Outcome.Shock * company.Proto.NewsShock * Size();
        if (shock != 0f)
            Announce(company, shock);
        _news.Add(new ExchangeNewsItem(Ticks, ExchangeNewsKind.Outcome, headline, MathF.Sign(story.Outcome.Shock), story.Company));
    }

    private ExchangeStoryOutcome PickOutcome(ExchangeStoryPrototype story)
    {
        var total = story.Outcomes.Sum(o => o.Weight);
        var roll = NextFloat() * total;
        foreach (var outcome in story.Outcomes)
        {
            roll -= outcome.Weight;
            if (roll <= 0f)
                return outcome;
        }

        return story.Outcomes[^1];
    }

    private string Pick(ProtoId<LocalizedDatasetPrototype> dataset)
    {
        var values = _proto.Index(dataset).Values;
        return values[Next(0, values.Count)];
    }

    /// <summary>
    /// How big this piece of news is compared to a typical one.
    /// </summary>
    private float Size() => 0.6f + 0.8f * NextFloat();

    private int NextNewsDelay() => Next(_settings.NewsMinTicks, _settings.NewsMaxTicks + 1);

    public float NextFloat() => (float) _random.NextDouble();

    public int Next(int min, int max) => _random.Next(min, max);

    private float Gaussian()
    {
        // Box-Muller.
        var u = 1.0 - _random.NextDouble();
        var v = _random.NextDouble();
        return (float) (Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v));
    }
}
