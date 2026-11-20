using Content.Shared._Starlight.Cargo.Exchange;

namespace Content.Server._Starlight.Cargo.Exchange;

/// <summary>
/// The prices of one round's market. Each company has a hidden value that shocks move and that drifts back to its
/// opening price, and a visible price that wanders randomly while closing part of the gap to that value every tick.
/// Everything random comes from one seed, so a market can be replayed.
/// </summary>
public sealed class ExchangeMarket
{
    public sealed class Company(ExchangeCompanyPrototype proto)
    {
        public readonly ExchangeCompanyPrototype Proto = proto;
        public float Price = proto.Price;
        public float Value = proto.Price;
        public readonly List<float> History = new() { proto.Price };
    }

    private readonly Random _random;
    private readonly List<(int Tick, string Company, float Shock)> _pending = new();

    /// <summary>
    /// Companies by prototype id, in id order so ticks are reproducible.
    /// </summary>
    public readonly SortedDictionary<string, Company> Companies = new();

    public int Ticks { get; private set; }

    public ExchangeMarket(IEnumerable<ExchangeCompanyPrototype> companies, int seed)
    {
        _random = new Random(seed);
        foreach (var proto in companies)
        {
            Companies[proto.ID] = new Company(proto);
        }
    }

    /// <summary>
    /// Moves a company's value by <paramref name="shock"/> (a share of it) at the start of a later tick.
    /// </summary>
    public void QueueShock(string company, float shock, int inTicks = 1)
    {
        _pending.Add((Ticks + inTicks, company, shock));
    }

    public void Tick(ExchangeSettingsPrototype settings)
    {
        Ticks++;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var (due, id, shock) = _pending[i];
            if (due > Ticks)
                continue;

            if (Companies.TryGetValue(id, out var shocked))
                shocked.Value = MathF.Max(ExchangeMath.MinPrice, shocked.Value * (1f + shock));
            _pending.RemoveAt(i);
        }

        foreach (var company in Companies.Values)
        {
            company.Value += settings.ValueReversion * (company.Proto.Price - company.Value);
            company.Price += settings.PriceReversion * (company.Value - company.Price)
                + company.Price * company.Proto.Volatility * Gaussian();
            company.Price = MathF.Max(ExchangeMath.MinPrice, company.Price);

            company.History.Add(company.Price);
            if (company.History.Count > settings.HistoryLength)
                company.History.RemoveAt(0);
        }
    }

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
