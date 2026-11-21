using Content.Shared.Cargo.Prototypes;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Cargo.Exchange;

/// <summary>
/// A company listed on the Guild Exchange. Its numbers stay the same every round so players can learn how it behaves.
/// </summary>
[Prototype]
public sealed partial class ExchangeCompanyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The short code shown on the ticker.
    /// </summary>
    [DataField(required: true)]
    public string Ticker = string.Empty;

    /// <summary>
    /// Its full name.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Its line of business, shown next to its name.
    /// </summary>
    [DataField(required: true)]
    public LocId Sector;

    /// <summary>
    /// The price it opens at, and the value it drifts back to over the round.
    /// </summary>
    [DataField(required: true)]
    public float Price;

    /// <summary>
    /// Its own random step each tick, as a share of the price, on top of what its sectors do.
    /// </summary>
    [DataField]
    public float Volatility = 0.01f;

    /// <summary>
    /// How strongly it follows each market factor. 1 moves with the factor, negative moves against it.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ExchangeFactorPrototype>, float> Factors = new();

    /// <summary>
    /// Gap between the buying and selling price, as a share of the price.
    /// </summary>
    [DataField]
    public float Spread = 0.01f;

    /// <summary>
    /// How much of the gap between price and value closes each tick. High means news is priced in fast.
    /// </summary>
    [DataField]
    public float PriceReversion = 0.3f;

    /// <summary>
    /// How much of last tick's move carries into this one. Trending names have more.
    /// </summary>
    [DataField]
    public float Momentum;

    /// <summary>
    /// How far its own headlines move its value, as a share of it.
    /// </summary>
    [DataField]
    public float NewsShock = 0.08f;

    /// <summary>
    /// Its own good headlines.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> GoodNews;

    /// <summary>
    /// Its own bad headlines.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> BadNews;

    /// <summary>
    /// Earnings a share expected from a normal quarter, in spesos.
    /// </summary>
    [DataField]
    public float Earnings = 5f;

    /// <summary>
    /// Value move per 100% earnings surprise.
    /// </summary>
    [DataField]
    public float EarningsSensitivity = 0.6f;

    /// <summary>
    /// Share of the price paid to shareholders at each dividend date. Zero pays none.
    /// </summary>
    [DataField]
    public float Dividend;

    /// <summary>
    /// Shares the rest of the galaxy trades in a quiet tick.
    /// </summary>
    [DataField]
    public float Volume = 2000f;
}

/// <summary>
/// Something many companies move with: the whole market, plasma prices, frontier demand and so on.
/// </summary>
[Prototype]
public sealed partial class ExchangeFactorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The tag its headlines carry on the wire.
    /// </summary>
    [DataField(required: true)]
    public string Tag = string.Empty;

    /// <summary>
    /// Its random step each tick.
    /// </summary>
    [DataField]
    public float Volatility = 0.004f;

    /// <summary>
    /// How far its headlines move it.
    /// </summary>
    [DataField]
    public float NewsShock = 0.05f;

    /// <summary>
    /// Its good headlines.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> GoodNews;

    /// <summary>
    /// Its bad headlines.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> BadNews;
}

/// <summary>
/// An announced station event that moves prices. Its headline is posted at once and the move lands on the next tick.
/// </summary>
[Prototype]
public sealed partial class ExchangeShockPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Game rules whose start sets off this shock.
    /// </summary>
    [DataField]
    public List<EntProtoId> Rules = new();

    [DataField(required: true)]
    public LocId Headline;

    /// <summary>
    /// Value moves per company, as a share of the value.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ExchangeCompanyPrototype>, float> Shocks = new();

    /// <summary>
    /// Moves per factor, passed on to every company by how strongly it follows that factor.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ExchangeFactorPrototype>, float> Factors = new();
}

/// <summary>
/// An early warning: a small headline that may turn into a big one a few minutes later, or into nothing.
/// </summary>
[Prototype]
public sealed partial class ExchangeStoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The first beat, easy to miss. Gets the company name as <c>company</c>.
    /// </summary>
    [DataField(required: true)]
    public LocId Warning;

    /// <summary>
    /// Fewest ticks before it plays out.
    /// </summary>
    [DataField]
    public int MinTicks = 4;

    /// <summary>
    /// Most ticks before it plays out.
    /// </summary>
    [DataField]
    public int MaxTicks = 10;

    /// <summary>
    /// How it can end. One is picked by weight when the warning goes out.
    /// </summary>
    [DataField(required: true)]
    public List<ExchangeStoryOutcome> Outcomes = new();
}

[DataDefinition]
public sealed partial class ExchangeStoryOutcome
{
    /// <summary>
    /// How likely this ending is against the story's others.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// The follow-up headline. None means the story fizzles out quietly.
    /// </summary>
    [DataField]
    public LocId? Headline;

    /// <summary>
    /// The value move, in multiples of the company's news shock.
    /// </summary>
    [DataField]
    public float Shock;
}

/// <summary>
/// Which company supplies each cargo product, so station orders show up in its order book.
/// </summary>
[Prototype]
public sealed partial class ExchangeSuppliersPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Supplier by cargo product category (its locale id).
    /// </summary>
    [DataField]
    public Dictionary<string, ProtoId<ExchangeCompanyPrototype>> Categories = new();

    /// <summary>
    /// Products that don't follow their category.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<CargoProductPrototype>, ProtoId<ExchangeCompanyPrototype>> Products = new();

    /// <summary>
    /// Supplier of everything else.
    /// </summary>
    [DataField]
    public ProtoId<ExchangeCompanyPrototype> Default = "NanoTrasen";
}

/// <summary>
/// The exchange's tuning. One of these, with the id Default, is used.
/// </summary>
[Prototype]
public sealed partial class ExchangeSettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Time between price ticks.
    /// </summary>
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Candles kept per company.
    /// </summary>
    [DataField]
    public int HistoryLength = 120;

    /// <summary>
    /// Closing prices shown in each watchlist sparkline.
    /// </summary>
    [DataField]
    public int SparkLength = 30;

    /// <summary>
    /// Charged on every trade, as a share of its value.
    /// </summary>
    [DataField]
    public float Fee = 0.022f;

    /// <summary>
    /// How much of the gap between a company's value and its opening price closes each tick.
    /// </summary>
    [DataField]
    public float ValueReversion = 0.02f;

    /// <summary>
    /// The most of cargo's budget plus investments that may be in shares or shorts, unless the quartermaster unlocks it.
    /// </summary>
    [DataField]
    public float CargoCap = 0.25f;

    /// <summary>
    /// Wait between one account's orders.
    /// </summary>
    [DataField]
    public TimeSpan TradeCooldown = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Limit orders an account can have resting at once.
    /// </summary>
    [DataField]
    public int MaxOpenOrders = 5;

    /// <summary>
    /// Ticks between news slots.
    /// </summary>
    [DataField]
    public int NewsMinTicks = 3;

    /// <summary>
    /// Most ticks between news slots.
    /// </summary>
    [DataField]
    public int NewsMaxTicks = 7;

    /// <summary>
    /// Share of news slots that are sector or economy news.
    /// </summary>
    [DataField]
    public float FactorNewsChance = 0.2f;

    /// <summary>
    /// Share of news slots that start an early-warning story.
    /// </summary>
    [DataField]
    public float StoryChance = 0.25f;

    /// <summary>
    /// Share of company news that comes out as an unconfirmed rumour.
    /// </summary>
    [DataField]
    public float RumourChance = 0.3f;

    /// <summary>
    /// Share of rumours that come to nothing.
    /// </summary>
    [DataField]
    public float FalseRumourChance = 0.4f;

    /// <summary>
    /// Share of a headline's move already in the price by the time it is on the wire. The rest follows over the next
    /// ticks, so reacting fast still pays, but less than seeing it coming.
    /// </summary>
    [DataField]
    public float NewsPricedIn = 0.35f;

    /// <summary>
    /// Chance the market shrugs a headline off and moves the other way, by half as much.
    /// </summary>
    [DataField]
    public float NewsFadeChance = 0.3f;

    /// <summary>
    /// Share of company news that uses the general business lines instead of the company's own.
    /// </summary>
    [DataField]
    public float GeneralNewsChance = 0.3f;

    /// <summary>
    /// First tick an earnings report can be scheduled for.
    /// </summary>
    [DataField]
    public int EarningsMinTick = 16;

    /// <summary>
    /// Last tick a first earnings report can be scheduled for.
    /// </summary>
    [DataField]
    public int EarningsMaxTick = 110;

    /// <summary>
    /// Chance of a second report later in the round.
    /// </summary>
    [DataField]
    public float SecondReportChance = 0.5f;

    /// <summary>
    /// Spread of analysts' consensus around the normal result.
    /// </summary>
    [DataField]
    public float ConsensusNoise = 0.05f;

    /// <summary>
    /// Luck in the actual result.
    /// </summary>
    [DataField]
    public float EarningsNoise = 0.06f;

    /// <summary>
    /// How much of a quarter's news (summed value moves) shows up in earnings.
    /// </summary>
    [DataField]
    public float EarningsFromNews = 1.5f;

    /// <summary>
    /// Surprises inside this band are reported as in line.
    /// </summary>
    [DataField]
    public float InLineBand = 0.02f;

    /// <summary>
    /// The biggest value move an earnings surprise can cause.
    /// </summary>
    [DataField]
    public float EarningsMaxShock = 0.4f;

    /// <summary>
    /// Ticks between dividend dates.
    /// </summary>
    [DataField]
    public int DividendEvery = 40;

    /// <summary>
    /// Shorts must put up this share of what they sell as extra collateral.
    /// </summary>
    [DataField]
    public float ShortMargin = 0.5f;

    /// <summary>
    /// A short is covered by force once its collateral is less than this many times the cost of buying back.
    /// </summary>
    [DataField]
    public float MaintenanceMargin = 1.2f;

    /// <summary>
    /// Borrowing fee per tick, as a share of the borrowed shares' value.
    /// </summary>
    [DataField]
    public float BorrowFee = 0.001f;

    /// <summary>
    /// Spesos of cargo orders that push a supplier's order premium most of the way to its cap.
    /// </summary>
    [DataField]
    public float OrderScale = 60_000f;

    /// <summary>
    /// The most cargo orders can lift a supplier's value, as a share of it.
    /// </summary>
    [DataField]
    public float OrderMaxBoost = 0.1f;

    /// <summary>
    /// The most the order premium can grow in one tick.
    /// </summary>
    [DataField]
    public float OrderTickCap = 0.02f;

    /// <summary>
    /// Order premium at which the wire notices.
    /// </summary>
    [DataField]
    public float OrderHeadlineAt = 0.025f;

    /// <summary>
    /// How much of the order premium shows up in earnings.
    /// </summary>
    [DataField]
    public float EarningsFromOrders = 1f;

    /// <summary>
    /// Headlines kept on the wire.
    /// </summary>
    [DataField]
    public int NewsKept = 40;

    /// <summary>
    /// Trades kept on the tape.
    /// </summary>
    [DataField]
    public int TradeLogKept = 30;

    /// <summary>
    /// Good headlines any company can get.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> GeneralGoodNews = "GuildExchangeNewsAnyGood";

    /// <summary>
    /// Bad headlines any company can get.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> GeneralBadNews = "GuildExchangeNewsAnyBad";

    /// <summary>
    /// Headlines for a supplier busy with station orders.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> OrderNews = "GuildExchangeNewsOrders";
}
