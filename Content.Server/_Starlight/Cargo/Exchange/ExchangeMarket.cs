using System.Linq;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Cargo.Exchange;

/// <summary>
/// Something the market put on the wire. <see cref="Text"/> is a locale id. <see cref="Moves"/> are the value moves a
/// station event sets off, by company.
/// </summary>
public sealed record ExchangeNewsItem(
    ExchangeNewsKind Kind,
    string Text,
    string? Company = null,
    string? Factor = null,
    float Actual = 0f,
    float Consensus = 0f,
    float Amount = 0f,
    IReadOnlyDictionary<string, float>? Moves = null);

/// <summary>
/// A scheduled earnings report. The consensus is known from the start; the result is made when it comes out.
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
/// opening price, and a price that closes part of the gap each tick on top of its own and its sectors' noise.
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

    private readonly IRobustRandom _random;
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
    /// Companies by prototype id, kept in order so the same dice always play out the same.
    /// </summary>
    public readonly SortedDictionary<string, Company> Companies = new();

    /// <summary>
    /// This round's earnings calendar, soonest first.
    /// </summary>
    public readonly List<ExchangeEarningsReport> Earnings = new();

    public int Ticks { get; private set; }

    public ExchangeSettingsPrototype Settings => _settings;

    public ExchangeMarket(IPrototypeManager proto, ExchangeSettingsPrototype settings, IRobustRandom random)
    {
        _proto = proto;
        _settings = settings;
        _random = random;
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

    private void QueueShock(string company, float shock)
    {
        _pending.Add((Ticks + 1, company, shock));
    }

    private void QueueFactorShock(string factor, float shock)
    {
        _pendingFactors.Add((Ticks + 1, factor, shock));
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

        _news.Add(new ExchangeNewsItem(ExchangeNewsKind.Event, shock.Headline, Moves: moves));
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
            _news.Add(new ExchangeNewsItem(ExchangeNewsKind.Orders, Pick(_settings.OrderNews), companyId));
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
            _news.Add(new ExchangeNewsItem(ExchangeNewsKind.Dividend, "guild-exchange-dividend", company.Proto.ID,
                Amount: amount));
        }
    }

    private void ScheduleEarnings()
    {
        foreach (var company in Companies.Values)
        {
            var first = _random.Next(_settings.EarningsMinTick, _settings.EarningsMaxTick + 1);
            Earnings.Add(new ExchangeEarningsReport(company.Proto.ID, first, Consensus(company)));

            var second = first + _random.Next(40, 70);
            if (_random.NextFloat() < _settings.SecondReportChance)
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

        var text = MathF.Abs(surprise) < _settings.InLineBand ? "guild-exchange-earnings-inline"
            : surprise > 0 ? "guild-exchange-earnings-beat"
            : "guild-exchange-earnings-miss";
        _news.Add(new ExchangeNewsItem(ExchangeNewsKind.Earnings, text, company.Proto.ID,
            Actual: actual, Consensus: report.Consensus));
    }

    private void News()
    {
        var roll = _random.NextFloat();
        if (roll < _settings.FactorNewsChance && _factors.Count > 0)
        {
            var factor = _factors[_random.Next(0, _factors.Count)];
            var good = _random.NextFloat() < 0.5f;
            AnnounceFactor(factor.ID, (good ? 1f : -1f) * factor.NewsShock * Size());
            _news.Add(new ExchangeNewsItem(ExchangeNewsKind.Factor, Pick(good ? factor.GoodNews : factor.BadNews),
                Factor: factor.ID));
            return;
        }

        var company = Companies.Values.ElementAt(_random.Next(0, Companies.Count));
        if (roll < _settings.FactorNewsChance + _settings.StoryChance && _storyProtos.Count > 0)
        {
            var story = _storyProtos[_random.Next(0, _storyProtos.Count)];
            var outcome = PickOutcome(story);
            _stories.Add(new PendingStory(Ticks + _random.Next(story.MinTicks, story.MaxTicks + 1), company.Proto.ID, outcome));
            _news.Add(new ExchangeNewsItem(ExchangeNewsKind.Warning, story.Warning, company.Proto.ID));
            return;
        }

        var positive = _random.NextFloat() < 0.5f;
        var general = _random.NextFloat() < _settings.GeneralNewsChance;
        var dataset = general
            ? positive ? _settings.GeneralGoodNews : _settings.GeneralBadNews
            : positive ? company.Proto.GoodNews : company.Proto.BadNews;
        var rumour = _random.NextFloat() < _settings.RumourChance;
        var text = Pick(dataset);
        var shock = (positive ? 1f : -1f) * company.Proto.NewsShock * Size();

        _news.Add(new ExchangeNewsItem(rumour ? ExchangeNewsKind.Rumour : ExchangeNewsKind.Company, text, company.Proto.ID));

        if (rumour && _random.NextFloat() < _settings.FalseRumourChance)
            return;

        Announce(company, shock);
    }

    private void Announce(Company company, float shock)
    {
        var (now, later) = SplitHeadline(shock);
        MoveNow(company, now);
        QueueShock(company.Proto.ID, later);
    }

    private void AnnounceFactor(string factor, float shock)
    {
        var (now, later) = SplitHeadline(shock);
        foreach (var company in Companies.Values)
        {
            if (company.Proto.Factors.TryGetValue(factor, out var loading))
                MoveNow(company, now * loading);
        }

        QueueFactorShock(factor, later);
    }

    /// <summary>
    /// A headline's move: some of it is in the price already, the rest lands next tick, and now and then the market
    /// shrugs it off and goes the other way.
    /// </summary>
    private (float Now, float Later) SplitHeadline(float shock)
    {
        var real = _random.NextFloat() < _settings.NewsFadeChance ? -0.5f * shock : shock;
        var now = real * _settings.NewsPricedIn;
        return (now, real - now);
    }

    private static void MoveNow(Company company, float change)
    {
        company.Value = MathF.Max(ExchangeMath.MinPrice, company.Value * (1f + change));
        company.Price = MathF.Max(ExchangeMath.MinPrice, company.Price * (1f + change));
        company.QuarterNews += change;
    }

    private void ResolveStory(PendingStory story)
    {
        if (story.Outcome.Headline is not { } headline || !Companies.TryGetValue(story.Company, out var company))
            return;

        var shock = story.Outcome.Shock * company.Proto.NewsShock * Size();
        if (shock != 0f)
            Announce(company, shock);
        _news.Add(new ExchangeNewsItem(ExchangeNewsKind.Outcome, headline, story.Company));
    }

    private ExchangeStoryOutcome PickOutcome(ExchangeStoryPrototype story)
    {
        var total = story.Outcomes.Sum(o => o.Weight);
        var roll = _random.NextFloat() * total;
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
        return values[_random.Next(0, values.Count)];
    }

    /// <summary>
    /// How big this piece of news is compared to a typical one.
    /// </summary>
    private float Size() => 0.6f + 0.8f * _random.NextFloat();

    private int NextNewsDelay() => _random.Next(_settings.NewsMinTicks, _settings.NewsMaxTicks + 1);

    private float Gaussian() => (float) _random.NextGaussian();
}
