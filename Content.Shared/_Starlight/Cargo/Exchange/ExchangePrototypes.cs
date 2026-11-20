using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Cargo.Exchange;

/// <summary>
/// A company listed on the Guild Exchange.
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

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// One line about what the company does.
    /// </summary>
    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public LocId Sector;

    /// <summary>
    /// The price it opens at, and the value it drifts back to over the round.
    /// </summary>
    [DataField(required: true)]
    public float Price;

    /// <summary>
    /// Size of the random step each tick, as a share of the price.
    /// </summary>
    [DataField]
    public float Volatility = 0.015f;

    /// <summary>
    /// How many shares it takes to move the price by its whole value; bigger means trades move it less.
    /// </summary>
    [DataField]
    public float Liquidity = 500f;

    /// <summary>
    /// How far a piece of sector news moves the company's value, as a share of it.
    /// </summary>
    [DataField]
    public float NewsShock = 0.08f;

    /// <summary>
    /// Sector headlines that push the price up. Each gets the company name as <c>company</c>.
    /// </summary>
    [DataField]
    public List<LocId> GoodNews = new();

    /// <summary>
    /// Sector headlines that push the price down.
    /// </summary>
    [DataField]
    public List<LocId> BadNews = new();
}

/// <summary>
/// Moves company prices when one of the listed game rules starts, after posting a headline.
/// </summary>
[Prototype]
public sealed partial class ExchangeShockPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Game rules that trigger it. Only rules the crew gets told about belong here, or the market would leak them.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Rules = new();

    [DataField(required: true)]
    public LocId Headline;

    /// <summary>
    /// How much each company's value changes, as a share of it.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ExchangeCompanyPrototype>, float> Shocks = new();
}

/// <summary>
/// The exchange's tunables.
/// </summary>
[Prototype]
public sealed partial class ExchangeSettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many past prices each company keeps for its chart.
    /// </summary>
    [DataField]
    public int HistoryLength = 40;

    /// <summary>
    /// Broker's fee on every trade, as a share of its value.
    /// </summary>
    [DataField]
    public float Fee = 0.03f;

    /// <summary>
    /// Gap between the buying and selling price, as a share of the price.
    /// </summary>
    [DataField]
    public float Spread = 0.02f;

    /// <summary>
    /// How much of the gap to its value the price closes each tick.
    /// </summary>
    [DataField]
    public float PriceReversion = 0.25f;

    /// <summary>
    /// How much of the gap to its opening price a company's value closes each tick, so shocks fade.
    /// </summary>
    [DataField]
    public float ValueReversion = 0.04f;

    /// <summary>
    /// Share of the cargo budget that may be tied up in shares at once.
    /// </summary>
    [DataField]
    public float CargoCap = 0.25f;

    [DataField]
    public TimeSpan TradeCooldown = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The most the exchange pays out beyond what it took in, per round. Past this the Guild halts sales.
    /// </summary>
    [DataField]
    public int ProfitPool = 5000;

    [DataField]
    public TimeSpan SectorNewsMin = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan SectorNewsMax = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Chance a sector story is only a rumour.
    /// </summary>
    [DataField]
    public float RumourChance = 0.4f;

    /// <summary>
    /// Chance a rumour turns out to be false.
    /// </summary>
    [DataField]
    public float FalseRumourChance = 0.25f;

    /// <summary>
    /// Chance a sector story is an investigation that gets its verdict a few ticks later.
    /// </summary>
    [DataField]
    public float ProbeChance = 0.15f;

    [DataField]
    public int ProbeMinTicks = 4;

    [DataField]
    public int ProbeMaxTicks = 8;

    [DataField]
    public int NewsKept = 30;

    [DataField]
    public int TradeLogKept = 15;
}
