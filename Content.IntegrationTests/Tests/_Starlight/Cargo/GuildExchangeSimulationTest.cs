#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._Starlight.Cargo.Exchange;
using Content.Shared._Starlight.Cargo.Exchange;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Cargo;

/// <summary>
/// Whole rounds of the Guild Exchange, played by scripted traders through the same trading rules the game uses. The
/// balance targets: careless clicking loses about a third of the stake in an hour, an attentive casual player makes
/// about a fifth most rounds, and a skilled player about three fifths nearly every round.
/// </summary>
[TestFixture]
public sealed class GuildExchangeSimulationTest
{
    private const int Rounds = 400;
    private const int Ticks = 120;
    private const long Budget = 100_000;
    private const double RandomClickChance = 0.3;
    private const float CasualSize = 0.45f;

    private sealed class Purse(long balance) : IExchangeWallet
    {
        public long Balance { get; private set; } = balance;

        public void Add(long amount) => Balance += amount;
    }

    private enum Tier
    {
        Random,
        Casual,
        Skilled,
        EarningsAndEvents,
        RandomShorts,
    }

    /// <summary>
    /// One simulated round: a market, an account trading without the cargo cap, and the trader's own notes.
    /// </summary>
    private sealed class Sim(ExchangeMarket market, int seed)
    {
        public readonly ExchangeMarket Market = market;
        public readonly ExchangeAccount Account = new(true, "sim");
        public readonly Purse Purse = new(Budget);
        public readonly System.Random Dice = new(seed);
        public readonly Dictionary<string, int> CloseAt = new();
        public readonly Dictionary<string, float> Tally = new();
        public int Tick;
        public int MarginCalls;

        public bool Holds(string company) => Account.Longs.ContainsKey(company) || Account.Shorts.ContainsKey(company);

        public int Open => Account.Longs.Count + Account.Shorts.Count;

        public void Close(string company)
        {
            if (Account.Longs.TryGetValue(company, out var held))
                ExchangeLedger.Sell(Market, Account, Purse, company, held.Shares);
            if (Account.Shorts.TryGetValue(company, out var shorted))
                ExchangeLedger.Cover(Market, Account, Purse, company, shorted.Shares);
            CloseAt.Remove(company);
        }

        /// <summary>
        /// Takes a position in a direction with a share of the purse, closing anything the other way first.
        /// </summary>
        public void Take(string company, int direction, float size, int hold)
        {
            if (direction == 0 || !Market.Companies.TryGetValue(company, out var c))
                return;

            var isLong = Account.Longs.ContainsKey(company);
            var isShort = Account.Shorts.ContainsKey(company);
            if (isLong && direction > 0 || isShort && direction < 0)
            {
                CloseAt[company] = Math.Max(CloseAt.GetValueOrDefault(company), Tick + hold);
                return;
            }

            if (isLong || isShort)
                Close(company);

            var money = (long) (Purse.Balance * size);
            if (direction > 0)
            {
                var shares = ExchangeMath.MaxAffordable(c.Price, money, c.Proto.Spread, Market.Settings.Fee);
                if (shares > 0 && ExchangeLedger.Buy(Market, Account, Purse, company, shares).Ok)
                    CloseAt[company] = Tick + hold;
            }
            else
            {
                var shares = (int) (money / (Market.Settings.ShortMargin * c.Price));
                if (shares > 0 && ExchangeLedger.Short(Market, Account, Purse, company, shares).Ok)
                    CloseAt[company] = Tick + hold;
            }
        }
    }

    private static double PlayRound(IPrototypeManager proto, ExchangeSettingsPrototype settings, Tier tier, int seed)
    {
        var sim = new Sim(new ExchangeMarket(proto, settings, seed), seed * 7919 + (int) tier);
        var leans = proto.EnumeratePrototypes<ExchangeStoryPrototype>()
            .ToDictionary(s => s.ID, s => s.Outcomes.Sum(o => o.Weight * o.Shock) / s.Outcomes.Sum(o => o.Weight));

        for (sim.Tick = 1; sim.Tick <= Ticks; sim.Tick++)
        {
            sim.Market.Tick();
            ExchangeLedger.ChargeBorrowFees(sim.Market, sim.Account);
            foreach (var (company, _) in ExchangeLedger.MarginCalls(sim.Market, sim.Account, sim.Purse))
            {
                sim.MarginCalls++;
                sim.CloseAt.Remove(company);
            }

            var news = sim.Market.TakeNews();
            foreach (var item in news.Where(n => n.Kind == ExchangeNewsKind.Dividend && n.Company != null))
            {
                ExchangeLedger.PayDividend(sim.Account, sim.Purse, item.Company!, item.Amount);
            }

            switch (tier)
            {
                case Tier.Random:
                    RandomClicker(sim, true);
                    break;
                case Tier.RandomShorts:
                    RandomClicker(sim, false);
                    break;
                case Tier.Casual:
                    Casual(sim, news);
                    break;
                case Tier.Skilled:
                    Skilled(sim, news, leans, true);
                    break;
                case Tier.EarningsAndEvents:
                    Skilled(sim, news, leans, false);
                    break;
            }

            foreach (var (company, at) in sim.CloseAt.ToList())
            {
                if (at <= sim.Tick)
                    sim.Close(company);
            }
        }

        ExchangeLedger.Settle(sim.Market, sim.Account, sim.Purse);
        return sim.Purse.Balance / (double) Budget - 1.0;
    }

    /// <summary>
    /// Clicks every minute or two: buys, sells, shorts and covers whatever, in whatever size, and holds until bored.
    /// </summary>
    private static void RandomClicker(Sim sim, bool mixed)
    {
        if (sim.Dice.NextDouble() > RandomClickChance)
            return;

        var companies = sim.Market.Companies.Keys.ToList();
        var company = companies[sim.Dice.Next(companies.Count)];
        var roll = sim.Dice.NextDouble();
        var size = 0.2f + 0.4f * (float) sim.Dice.NextDouble();

        if (!mixed)
        {
            if (sim.Account.Shorts.ContainsKey(company) && roll < 0.5)
                sim.Close(company);
            else if (!sim.Holds(company))
                sim.Take(company, -1, size, int.MaxValue / 2);
            return;
        }

        if (roll < 0.35)
            sim.Take(company, 1, size, int.MaxValue / 2);
        else if (roll < 0.6)
            sim.Close(Any(sim, sim.Account.Longs.Keys) ?? company);
        else if (roll < 0.8)
            sim.Take(company, -1, size, int.MaxValue / 2);
        else
            sim.Close(Any(sim, sim.Account.Shorts.Keys) ?? company);
    }

    private static string? Any(Sim sim, IEnumerable<string> held)
    {
        var list = held.ToList();
        return list.Count == 0 ? null : list[sim.Dice.Next(list.Count)];
    }

    /// <summary>
    /// Reads confirmed headlines and station events, buys good news, gets out on bad news, holds a few positions.
    /// No shorts, no rumours, no early warnings, no earnings calendar.
    /// </summary>
    private static void Casual(Sim sim, List<ExchangeNewsItem> news)
    {
        foreach (var item in news)
        {
            switch (item.Kind)
            {
                case ExchangeNewsKind.Company or ExchangeNewsKind.Outcome or ExchangeNewsKind.Orders when item.Company != null:
                    if (item.Sentiment > 0 && sim.Open < 3)
                        sim.Take(item.Company, 1, CasualSize, 8);
                    else if (item.Sentiment < 0 && sim.Account.Longs.ContainsKey(item.Company))
                        sim.Close(item.Company);
                    break;
                case ExchangeNewsKind.Event when item.Moves is { Count: > 0 } moves:
                    var best = moves.MaxBy(m => m.Value);
                    if (best.Value > 0.03f && sim.Open < 3)
                        sim.Take(best.Key, 1, CasualSize, 8);
                    foreach (var (company, move) in moves)
                    {
                        if (move < 0 && sim.Account.Longs.ContainsKey(company))
                            sim.Close(company);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Everything the casual player does, plus shorts on bad news, the earnings calendar against its own tally of the
    /// quarter, the known leans of early warnings, sector news through each company's links, and rumours at half size.
    /// With <paramref name="all"/> false it only plays earnings and station events.
    /// </summary>
    private static void Skilled(Sim sim, List<ExchangeNewsItem> news, Dictionary<string, float> leans, bool all)
    {
        var market = sim.Market;
        foreach (var item in news)
        {
            var company = item.Company;
            switch (item.Kind)
            {
                case ExchangeNewsKind.Company or ExchangeNewsKind.Outcome or ExchangeNewsKind.Orders when company != null:
                    Note(sim, company, item.Sentiment * market.Companies[company].Proto.NewsShock);
                    if (all)
                        sim.Take(company, item.Sentiment, 0.3f, 7);
                    break;
                case ExchangeNewsKind.Rumour when company != null && all:
                    sim.Take(company, item.Sentiment, 0.12f, 6);
                    break;
                case ExchangeNewsKind.Warning when company != null && item.Story != null && all:
                    var lean = leans.GetValueOrDefault(item.Story);
                    if (MathF.Abs(lean) > 0.25f)
                        sim.Take(company, MathF.Sign(lean), 0.12f, 12);
                    break;
                case ExchangeNewsKind.Earnings when company != null:
                    sim.Tally.Remove(company);
                    sim.Take(company, item.Sentiment, 0.3f, 6);
                    break;
                case ExchangeNewsKind.Factor when item.Factor != null && all:
                    var follower = market.Companies.Values
                        .Where(c => c.Proto.Factors.ContainsKey(item.Factor))
                        .MaxBy(c => MathF.Abs(c.Proto.Factors[item.Factor]));
                    if (follower != null)
                        sim.Take(follower.Proto.ID, item.Sentiment * MathF.Sign(follower.Proto.Factors[item.Factor]), 0.25f, 7);
                    break;
                case ExchangeNewsKind.Event when item.Moves is { Count: > 0 } moves:
                    foreach (var (moved, size) in moves)
                    {
                        Note(sim, moved, size);
                    }

                    var biggest = moves.MaxBy(m => MathF.Abs(m.Value));
                    if (MathF.Abs(biggest.Value) > 0.03f)
                        sim.Take(biggest.Key, MathF.Sign(biggest.Value), 0.3f, 8);
                    break;
            }
        }

        // Position ahead of tomorrow's reports when the quarter's news clearly points one way.
        foreach (var report in market.Earnings.Where(r => r.Tick == market.Ticks + 1))
        {
            var tally = sim.Tally.GetValueOrDefault(report.Company);
            if (MathF.Abs(tally) > 0.05f)
                sim.Take(report.Company, MathF.Sign(tally), 0.3f, 6);
        }
    }

    private static void Note(Sim sim, string company, float move)
    {
        sim.Tally[company] = sim.Tally.GetValueOrDefault(company) + move;
    }

    private sealed record Spread(double Median, double Profitable, double P10, double P25, double P90, double Above, double Below);

    private static Spread Measure(List<double> results, double above, double below)
    {
        var sorted = results.OrderBy(r => r).ToList();
        double At(double q) => sorted[(int) Math.Clamp(Math.Round(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
        return new Spread(At(0.5), sorted.Count(r => r > 0) / (double) sorted.Count, At(0.1), At(0.25), At(0.9),
            sorted.Count(r => r > above) / (double) sorted.Count, sorted.Count(r => r < below) / (double) sorted.Count);
    }

    /// <summary>
    /// How often the first trader's total over three rounds in a row beats the second's.
    /// </summary>
    private static double BeatsOverThreeRounds(List<double> better, List<double> worse)
    {
        var wins = 0;
        var runs = better.Count / 3;
        for (var i = 0; i < runs; i++)
        {
            if (better.Skip(i * 3).Take(3).Sum() > worse.Skip(i * 3).Take(3).Sum())
                wins++;
        }

        return wins / (double) runs;
    }

    private static string Describe(string name, Spread s, double above, double below)
    {
        return $"{name}: median {s.Median:+0%;-0%}, profitable {s.Profitable:0%}, p10 {s.P10:+0%;-0%}, p25 {s.P25:+0%;-0%}, " +
               $"p90 {s.P90:+0%;-0%}, above {above:+0%} {s.Above:0.0%}, below {below:+0%;-0%} {s.Below:0.0%}";
    }

    /// <summary>
    /// Plays <see cref="Rounds"/> seeded hour-long rounds per tier and checks each tier lands in its band.
    /// </summary>
    [Test]
    public async Task SkillTiersLandInTheirBands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var settings = proto.Index<ExchangeSettingsPrototype>("Default");
            List<double> Play(Tier tier) => Enumerable.Range(1, Rounds).Select(seed => PlayRound(proto, settings, tier, seed)).ToList();

            var randomRounds = Play(Tier.Random);
            var casualRounds = Play(Tier.Casual);
            var skilledRounds = Play(Tier.Skilled);
            var random = Measure(randomRounds, 0.5, -0.75);
            var casual = Measure(casualRounds, 1.0, -0.5);
            var skilled = Measure(skilledRounds, 2.0, -0.5);
            var casualOverRandom = BeatsOverThreeRounds(casualRounds, randomRounds);
            var skilledOverCasual = BeatsOverThreeRounds(skilledRounds, casualRounds);
            TestContext.Out.WriteLine(Describe("Random", random, 0.5, -0.75));
            TestContext.Out.WriteLine(Describe("Casual", casual, 1.0, -0.5));
            TestContext.Out.WriteLine(Describe("Skilled", skilled, 2.0, -0.5));
            TestContext.Out.WriteLine($"Over three rounds: casual beats random {casualOverRandom:0%}, skilled beats casual {skilledOverCasual:0%}");

            Assert.Multiple(() =>
            {
                Assert.That(random.Median, Is.InRange(-0.45, -0.30), "random median");
                Assert.That(random.Profitable, Is.InRange(0.06, 0.15), "random profitable share");
                Assert.That(random.P90, Is.LessThan(0.08), "random p90");
                Assert.That(random.Above, Is.LessThan(0.04), "random big wins");
                Assert.That(random.Below, Is.InRange(0.02, 0.10), "random blow-ups");

                Assert.That(casual.Median, Is.InRange(0.14, 0.30), "casual median");
                Assert.That(casual.Profitable, Is.InRange(0.66, 0.80), "casual profitable share");
                Assert.That(casual.Above, Is.LessThan(0.10), "casual big wins");

                Assert.That(skilled.Median, Is.InRange(0.50, 0.80), "skilled median");
                Assert.That(skilled.Profitable, Is.InRange(0.82, 0.93), "skilled profitable share");
                Assert.That(skilled.Below, Is.LessThan(0.03), "skilled blow-ups");

                Assert.That(casual.Median, Is.GreaterThan(random.P90), "casual clearly beats random");
                Assert.That(skilled.P25, Is.GreaterThan(casual.Median), "skilled clearly beats casual");
                Assert.That(casualOverRandom, Is.GreaterThan(0.9), "three rounds are enough to see casual beat random");
                Assert.That(skilledOverCasual, Is.GreaterThan(0.75), "three rounds are enough to see skilled beat casual");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Playing only the earnings calendar and announced station events already beats random clicking.
    /// </summary>
    [Test]
    public async Task EarningsAndEventsBeatRandom()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var settings = proto.Index<ExchangeSettingsPrototype>("Default");
            var informed = Measure(Enumerable.Range(1, Rounds).Select(s => PlayRound(proto, settings, Tier.EarningsAndEvents, s)).ToList(), 1, -0.5);
            var random = Measure(Enumerable.Range(1, Rounds).Select(s => PlayRound(proto, settings, Tier.Random, s)).ToList(), 1, -0.5);
            TestContext.Out.WriteLine(Describe("Earnings and events", informed, 1, -0.5));
            Assert.That(informed.Median, Is.GreaterThan(0), "profitable on its own");
            Assert.That(informed.Median, Is.GreaterThan(random.P90));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Shorting at random, holding through rallies into margin calls, loses on average.
    /// </summary>
    [Test]
    public async Task RandomShortingLoses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var settings = proto.Index<ExchangeSettingsPrototype>("Default");
            var results = Enumerable.Range(1, Rounds).Select(s => PlayRound(proto, settings, Tier.RandomShorts, s)).ToList();
            TestContext.Out.WriteLine(Describe("Random shorts", Measure(results, 0.5, -0.75), 0.5, -0.75));
            Assert.That(results.Average(), Is.LessThan(0));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Buying a supplier's shares and then ordering its crates to lift them costs more than it brings in, at any order
    /// size, even holding the whole of a large late-round budget in it.
    /// </summary>
    [Test]
    public async Task PumpingASupplierWithOrdersLoses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var settings = proto.Index<ExchangeSettingsPrototype>("Default");
            foreach (var position in new long[] { 100_000, 500_000 })
            {
                foreach (var spend in new[] { 2_000, 10_000, 40_000, 150_000 })
                {
                    double gained = 0;
                    const int seeds = 100;
                    for (var seed = 1; seed <= seeds; seed++)
                    {
                        gained += PumpCycle(proto, settings, seed, position, spend) - PumpCycle(proto, settings, seed, position, 0);
                    }

                    var average = gained / seeds;
                    TestContext.Out.WriteLine($"Position {position}, orders {spend}: shares gained {average:0} on average");
                    Assert.That(average, Is.LessThan(spend), $"position {position}, orders {spend}");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Buys DONK, orders crates from Donk Co. over a few ticks, sells. Returns what the shares made, before the orders' cost.
    /// </summary>
    private static long PumpCycle(IPrototypeManager proto, ExchangeSettingsPrototype settings, int seed, long position, int spend)
    {
        var market = new ExchangeMarket(proto, settings, seed);
        var account = new ExchangeAccount(true, "pump");
        var purse = new Purse(position);
        var donk = market.Companies["DonkCo"];
        ExchangeLedger.Buy(market, account, purse, "DonkCo",
            ExchangeMath.MaxAffordable(donk.Price, position, donk.Proto.Spread, settings.Fee));

        for (var tick = 0; tick < 10; tick++)
        {
            if (tick < 5 && spend > 0)
                market.RecordOrder("DonkCo", spend / 5);
            market.Tick();
            market.TakeNews();
        }

        ExchangeLedger.Settle(market, account, purse);
        return purse.Balance - position;
    }

    [Test]
    public async Task PricesRepeatFromASeed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var settings = proto.Index<ExchangeSettingsPrototype>("Default");
            var a = new ExchangeMarket(proto, settings, 1234);
            var b = new ExchangeMarket(proto, settings, 1234);
            var c = new ExchangeMarket(proto, settings, 1235);
            for (var i = 0; i < 60; i++)
            {
                a.Tick();
                b.Tick();
                c.Tick();
            }

            Assert.That(a.Companies, Has.Count.EqualTo(8));
            foreach (var (id, company) in a.Companies)
            {
                Assert.That(company.Candles, Is.EqualTo(b.Companies[id].Candles), $"{id} replays");
            }

            Assert.That(a.TakeNews().Select(n => n.Text), Is.EqualTo(b.TakeNews().Select(n => n.Text)), "the same news");
            Assert.That(a.Companies.Values.Select(x => x.Price), Is.Not.EqualTo(c.Companies.Values.Select(x => x.Price)));
        });

        await pair.CleanReturnAsync();
    }
}
