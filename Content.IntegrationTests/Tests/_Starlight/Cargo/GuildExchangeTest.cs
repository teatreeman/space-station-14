#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Starlight.Cargo.Exchange;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared._Starlight.Time;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Stacks;
using Content.Shared.Station.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Cargo;

[TestFixture]
public sealed class GuildExchangeTest : InteractionTest
{
    private const int Seed = 1234;

    // A full human, so an ID can be worn in the ID slot while a hand holds cash.
    protected override string PlayerPrototype => "MobHuman";

    private GuildExchangeSystem Exchange => SEntMan.System<GuildExchangeSystem>();

    private sealed class Purse(long balance) : IExchangeWallet
    {
        public long Balance { get; private set; } = balance;

        public void Add(long amount) => Balance += amount;
    }

    [Test]
    public async Task CargoTradesNeedCargoAccess()
    {
        var machine = await PoweredExchange();
        var station = await MakeStation();
        await PlaceInHands("AssistantIDCard");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "DonkCo", 5),
                Is.EqualTo(Loc.GetString("guild-exchange-no-access")));
        });

        await PlaceInHands("CargoIDCard");
        await Server.WaitAssertion(() =>
        {
            var cargo = SEntMan.System<CargoSystem>();
            var before = cargo.GetBalanceFromAccount(station, "Cargo");
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "DonkCo", 5), Is.Null);
            Assert.That(cargo.GetBalanceFromAccount(station, "Cargo"), Is.LessThan(before));
            Assert.That(Exchange.GetCargoAccount(machine, out _)!.Longs["DonkCo"].Shares, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task CargoCapAndCooldownHold()
    {
        var machine = await PoweredExchange();
        await MakeStation();
        await PlaceInHands("QuartermasterIDCard");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", OverCap()),
                Is.EqualTo(Loc.GetString("guild-exchange-cap")));

            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", 1), Is.Null);
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", 1),
                Is.EqualTo(Loc.GetString("guild-exchange-cooldown")));
        });
    }

    [Test]
    public async Task CargoShortsCountTowardTheCap()
    {
        var machine = await PoweredExchange();
        await MakeStation();
        await PlaceInHands("CargoIDCard");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Exchange.TryOrder(machine, SPlayer, ExchangeAccountKind.Cargo, ExchangeOrderSide.Short, "NanoTrasen", OverCap()),
                Is.EqualTo(Loc.GetString("guild-exchange-cap")));
            Assert.That(Exchange.TryOrder(machine, SPlayer, ExchangeAccountKind.Cargo, ExchangeOrderSide.Short, "NanoTrasen", 1), Is.Null);
        });
    }

    [Test]
    public async Task CargoStaffStayCappedAndCannotUnlock()
    {
        var machine = await PoweredExchange();
        await MakeStation();
        await PlaceInHands("CargoIDCard");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Exchange.SetFullBudget(machine, SPlayer, true), Is.EqualTo(Loc.GetString("guild-exchange-full-budget-denied")));
            Assert.That(machine.Comp.FullBudgetBy, Is.Null);
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", OverCap()),
                Is.EqualTo(Loc.GetString("guild-exchange-cap")));
        });
    }

    [Test]
    public async Task QuartermasterCanUnlockTheWholeBudget()
    {
        var machine = await PoweredExchange();
        await MakeStation();
        await PlaceInHands("QuartermasterIDCard");

        await Server.WaitAssertion(() =>
        {
            var overCap = OverCap();
            Assert.That(Exchange.SetFullBudget(machine, SPlayer, true), Is.Null);
            Assert.That(machine.Comp.FullBudgetBy, Is.EqualTo(SPlayer));
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", overCap), Is.Null,
                "past a quarter of the budget once unlocked");

            Assert.That(Exchange.SetFullBudget(machine, SPlayer, false), Is.Null);
            Assert.That(machine.Comp.FullBudgetBy, Is.Null, "toggling it off locks it");
            Exchange.GetCargoAccount(machine, out _)!.NextTrade = TimeSpan.Zero;
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", overCap),
                Is.EqualTo(Loc.GetString("guild-exchange-cap")));
        });
    }

    [Test]
    public async Task WholeBudgetLocksWhenClosedOrSomeoneElseTrades()
    {
        var machine = await PoweredExchange();
        await MakeStation();
        await PlaceInHands("QuartermasterIDCard");

        await Server.WaitAssertion(() =>
        {
            var ui = SEntMan.System<SharedUserInterfaceSystem>();
            ui.OpenUi(machine.Owner, GuildExchangeUiKey.Key, SPlayer);
            Assert.That(Exchange.SetFullBudget(machine, SPlayer, true), Is.Null);
            ui.CloseUi(machine.Owner, GuildExchangeUiKey.Key, SPlayer);
            Assert.That(machine.Comp.FullBudgetBy, Is.Null, "closing the window locks it");

            Assert.That(Exchange.SetFullBudget(machine, SPlayer, true), Is.Null);
            var tech = SEntMan.SpawnEntity("MobHuman", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            var id = SEntMan.SpawnEntity("CargoIDCard", SEntMan.GetComponent<TransformComponent>(tech).Coordinates);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(tech, id, "id", silent: true, force: true));
            Assert.That(Exchange.TryTrade(machine, tech, ExchangeAccountKind.Cargo, "DonkCo", 1), Is.Null);
            Assert.That(machine.Comp.FullBudgetBy, Is.Null, "someone else trading cargo locks it");
        });
    }

    /// <summary>
    /// NanoTrasen shares costing more than cargo may still invest under the cap, but less than its whole budget.
    /// </summary>
    private int OverCap()
    {
        var account = Exchange.GetCargoAccount(STarget!.Value, out var station)!;
        var balance = SEntMan.System<CargoSystem>().GetBalanceFromAccount(station, "Cargo");
        var company = Exchange.Market!.Companies["NanoTrasen"];
        var settings = Exchange.Settings;
        var shares = ExchangeMath.MaxAffordable(company.Price, (long) (balance * 0.4f), company.Proto.Spread, settings.Fee);
        Assert.That(ExchangeMath.Buy(company.Price, shares, company.Proto.Spread, settings.Fee),
            Is.GreaterThan(ExchangeLedger.CapLeft(account, balance, settings.CargoCap)));
        return shares;
    }

    [Test]
    public async Task PersonalCashGoesInAndComesOutOnce()
    {
        var machine = await PoweredExchange();
        // Worn in the ID slot, the way people carry it, so the hand is free for the cash.
        await Server.WaitPost(() =>
        {
            var id = SEntMan.SpawnEntity("AssistantIDCard", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(SPlayer, id, "id", silent: true, force: true));
        });
        var cash = await PlaceInHands("SpaceCash", 100);

        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(ToServer(cash)) || SEntMan.IsQueuedForDeletion(ToServer(cash)), "the cash went in");
        });

        await Server.WaitAssertion(() =>
        {
            var account = Exchange.GetPersonalAccount(SPlayer)!;
            Assert.That(account.Cash, Is.EqualTo(100));
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Personal, "DonkCo", 1), Is.Null);
            account.NextTrade = TimeSpan.Zero;
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Personal, "DonkCo", -1), Is.Null);
            var left = account.Cash;
            Assert.That(left, Is.InRange(1, 99), "a round trip costs the spread and fee");

            Assert.That(Exchange.Withdraw(machine, SPlayer), Is.Null);
            Assert.That(account.Cash, Is.EqualTo(0));
            Assert.That(Exchange.Withdraw(machine, SPlayer), Is.EqualTo(Loc.GetString("guild-exchange-no-funds")), "paid out once");

            var paidOut = SEntMan.EntityQuery<StackComponent>()
                .Where(s => s.StackTypeId == "Credit" && SEntMan.GetComponent<TransformComponent>(s.Owner).Coordinates.Equals(SEntMan.GetComponent<TransformComponent>(machine).Coordinates))
                .Sum(s => s.Count);
            Assert.That(paidOut, Is.EqualTo(left));
        });
    }

    [Test]
    public async Task ShortingAFallingStockProfitsAndARisingOneIsCalled()
    {
        await PoweredExchange();
        await Server.WaitAssertion(() =>
        {
            var market = Exchange.Market!;
            var nt = market.Companies["NanoTrasen"];

            var falling = new ExchangeAccount(false, "bear");
            var purse = new Purse(10_000);
            nt.Price = 100f;
            Assert.That(ExchangeLedger.Short(market, falling, purse, "NanoTrasen", 50).Ok);
            Assert.That(purse.Balance, Is.EqualTo(10_000 - 2_500), "half the value put up as margin");
            nt.Price = 80f;
            Assert.That(ExchangeLedger.Cover(market, falling, purse, "NanoTrasen", 50).Ok);
            Assert.That(falling.Shorts, Is.Empty, "the borrowed shares went back");
            Assert.That(purse.Balance, Is.GreaterThan(10_000), "a falling stock pays the short");

            var rising = new ExchangeAccount(false, "squeezed");
            var wallet = new Purse(10_000);
            nt.Price = 100f;
            Assert.That(ExchangeLedger.Short(market, rising, wallet, "NanoTrasen", 50).Ok);
            nt.Price = 115f;
            Assert.That(ExchangeLedger.MarginCalls(market, rising, wallet), Is.Empty, "still above the maintenance margin");
            nt.Price = 140f;
            var called = ExchangeLedger.MarginCalls(market, rising, wallet);
            Assert.That(called, Has.Count.EqualTo(1), "covered by force past the margin");
            Assert.That(rising.Shorts, Is.Empty);
            Assert.That(wallet.Balance, Is.LessThan(10_000), "a rally costs the short");
            Assert.That(rising.WrittenOff, Is.Zero, "the collateral covered it");
        });
    }

    [Test]
    public async Task TradingCreatesNoMoneyOrShares()
    {
        await PoweredExchange();
        await Server.WaitAssertion(() =>
        {
            var market = Exchange.Market!;
            var account = new ExchangeAccount(false, "churn");
            var purse = new Purse(50_000);
            var dice = new System.Random(Seed);
            var companies = market.Companies.Keys.ToList();

            // Prices never move here, so every spesos that leaves is a cost and none may come back extra.
            for (var i = 0; i < 300; i++)
            {
                var company = companies[dice.Next(companies.Count)];
                var shares = dice.Next(1, 40);
                switch (dice.Next(4))
                {
                    case 0: ExchangeLedger.Buy(market, account, purse, company, shares); break;
                    case 1: ExchangeLedger.Sell(market, account, purse, company, shares); break;
                    case 2: ExchangeLedger.Short(market, account, purse, company, shares); break;
                    default: ExchangeLedger.Cover(market, account, purse, company, shares); break;
                }

                ExchangeLedger.ChargeBorrowFees(market, account);
            }

            ExchangeLedger.Settle(market, account, purse);
            Assert.That(account.Longs, Is.Empty);
            Assert.That(account.Shorts, Is.Empty, "every borrowed share returned");
            Assert.That(purse.Balance, Is.LessThan(50_000), "costs only, no free money");
            Assert.That(purse.Balance, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task LimitOrdersWaitForTheirPrice()
    {
        await PoweredExchange();
        await Server.WaitAssertion(() =>
        {
            var market = Exchange.Market!;
            var donk = market.Companies["DonkCo"];
            var account = new ExchangeAccount(false, "patient");
            var purse = new Purse(10_000);
            donk.Price = 40f;

            Assert.That(ExchangeLedger.PlaceLimit(market, account, "DonkCo", true, 10, 35f, EntityUid.Invalid).Ok);
            Assert.That(ExchangeLedger.FillLimits(market, account, purse, () => null), Is.Empty, "not reached");

            donk.Price = 34f;
            var filled = ExchangeLedger.FillLimits(market, account, purse, () => null);
            Assert.That(filled, Has.Count.EqualTo(1));
            Assert.That(filled[0].Result.Ok);
            Assert.That(account.Longs["DonkCo"].Shares, Is.EqualTo(10));
            Assert.That(account.Orders, Is.Empty);

            for (var i = 0; i < market.Settings.MaxOpenOrders; i++)
            {
                Assert.That(ExchangeLedger.PlaceLimit(market, account, "DonkCo", false, 1, 100f + i, EntityUid.Invalid).Ok);
            }

            Assert.That(ExchangeLedger.PlaceLimit(market, account, "DonkCo", false, 1, 200f, EntityUid.Invalid).Error,
                Is.EqualTo("guild-exchange-too-many-orders"));
            Assert.That(ExchangeLedger.CancelLimit(account, account.Orders[0].Id));
            Assert.That(account.Orders, Has.Count.EqualTo(market.Settings.MaxOpenOrders - 1));
        });
    }

    [Test]
    public async Task DividendsReachHolders()
    {
        await PoweredExchange();
        await Server.WaitAssertion(() =>
        {
            var market = Exchange.Market!;
            var account = new ExchangeAccount(false, "holder");
            var purse = new Purse(10_000);
            Assert.That(ExchangeLedger.Buy(market, account, purse, "MedTak", 20).Ok);
            var before = purse.Balance;

            market.TakeNews();
            ExchangeNewsItem? dividend = null;
            while (dividend == null && market.Ticks <= market.Settings.DividendEvery)
            {
                market.Tick();
                dividend = market.TakeNews().FirstOrDefault(n => n.Kind == ExchangeNewsKind.Dividend && n.Company == "MedTak");
            }

            Assert.That(dividend, Is.Not.Null, "MedTak pays on the dividend date");
            ExchangeLedger.PayDividend(account, purse, "MedTak", dividend!.Amount);
            Assert.That(purse.Balance, Is.EqualTo(before + (long) MathF.Floor(20 * dividend.Amount)));
        });
    }

    [Test]
    public async Task EarningsFollowTheQuarter()
    {
        await PoweredExchange();
        await Server.WaitAssertion(() =>
        {
            var market = Exchange.Market!;
            Assert.That(market.Earnings.Select(e => e.Company).Distinct().Count(), Is.EqualTo(8), "everyone reports");
            Assert.That(market.Earnings.All(e => e.Consensus > 0 && e.Actual == null), "consensus is known before");

            var report = market.Earnings[0];
            var company = market.Companies[report.Company];
            while (market.Ticks < report.Tick)
            {
                company.QuarterNews = 0.4f;
                market.Tick();
            }

            Assert.That(report.Actual, Is.GreaterThan(report.Consensus), "a strong quarter beats");
            Assert.That(market.TakeNews().Any(n => n.Kind == ExchangeNewsKind.Earnings && n.Company == report.Company && n.Sentiment > 0));
        });
    }

    [Test]
    public async Task PaidCargoOrdersLiftTheirSupplier()
    {
        await PoweredExchange();
        CargoOrderData? paid = null;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Exchange.SupplierOf("FoodPizza"), Is.EqualTo("DonkCo"), "by category");
            Assert.That(Exchange.SupplierOf("EmergencyBruteKit"), Is.EqualTo("MedTak"), "by product");
            Assert.That(Exchange.SupplierOf("CargoMailCart"), Is.EqualTo("Vulcan"));

            var unpaid = new CargoOrderData(1, "FoodPizza", 5, "test", "test", "Cargo") { Price = 900 };
            paid = new CargoOrderData(2, "FoodPizza", 5, "test", "test", "Cargo") { Price = 900 };
            foreach (var order in new[] { unpaid, paid })
            {
                var ev = new FulfillCargoOrderEvent((EntityUid.Invalid, new StationDataComponent()), order,
                    (EntityUid.Invalid, new CargoOrderConsoleComponent()));
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
            }

            // Cargo charges after offering the order; only then is it approved.
            paid.Approved = true;
        });

        await RunTicks(1);
        await Server.WaitAssertion(() =>
        {
            var donk = Exchange.Market!.Companies["DonkCo"];
            var expected = 4500f / Exchange.Settings.OrderScale * Exchange.Settings.OrderMaxBoost;
            Assert.That(donk.OrderBoost, Is.EqualTo(expected).Within(1e-4f), "only the paid order counts");
        });
    }

    [Test]
    public async Task RoundEndClosesEverything()
    {
        var machine = await PoweredExchange();
        var station = await MakeStation();
        await PlaceInHands("QuartermasterIDCard");

        await Server.WaitAssertion(() =>
        {
            var cargo = SEntMan.System<CargoSystem>();
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "MedTak", 5), Is.Null);
            Exchange.GetCargoAccount(machine, out _)!.NextTrade = TimeSpan.Zero;
            Assert.That(Exchange.TryOrder(machine, SPlayer, ExchangeAccountKind.Cargo, ExchangeOrderSide.Short, "DonkCo", 5), Is.Null);
            var before = cargo.GetBalanceFromAccount(station, "Cargo");

            var ev = new RoundEndTextAppendEvent();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ev);

            var account = Exchange.GetCargoAccount(machine, out _)!;
            Assert.That(account.Longs, Is.Empty);
            Assert.That(account.Shorts, Is.Empty, "shorts covered at the bell");
            Assert.That(cargo.GetBalanceFromAccount(station, "Cargo"), Is.GreaterThan(before), "money back in the budget");
            Assert.That(ev.Text, Does.Contain(Exchange.StationTime(Server.Timing.CurTime)), "the bell rings in station time");
        });
    }

    [Test]
    public async Task TimesAreStationTime()
    {
        await PoweredExchange();
        await Server.WaitAssertion(() =>
        {
            var clock = SEntMan.System<SharedTimeSystem>();
            var now = Server.Timing.CurTime;
            Assert.That(clock.GetStationTime(now), Is.EqualTo(clock.GetStationTime()), "same clock as the PDA");
            Assert.That(ExchangeTime.Format(clock, now), Is.EqualTo(clock.GetStationTime().Time.ToString(@"hh\:mm")));
            Assert.That(Exchange.StationTime(now), Is.EqualTo(ExchangeTime.Format(clock, now)));

            Exchange.ApplyShock(ProtoMan.Index<ExchangeShockPrototype>("PizzaParty"));
            Assert.That(Exchange.NewsWire[0].Time, Is.EqualTo(now), "wire stories carry game time, shown as station time");
        });
    }

    [Test]
    public async Task ShocksLandAfterTheirHeadline()
    {
        await PoweredExchange();
        await Server.WaitAssertion(() =>
        {
            var shock = ProtoMan.Index<ExchangeShockPrototype>("MeteorsLarge");
            var vulcan = Exchange.Market!.Companies["Vulcan"];
            Exchange.ApplyShock(shock);

            Assert.That(Exchange.NewsWire[0].Text, Is.EqualTo(Loc.GetString(shock.Headline)), "headline first");
            var before = vulcan.Value;

            Exchange.Tick();
            Assert.That(vulcan.Value, Is.LessThan(before * 0.9f));
            for (var i = 0; i < 6; i++)
            {
                Exchange.Tick();
            }

            Assert.That(vulcan.Price, Is.LessThan(vulcan.Proto.Price), "the price followed it down");
        });
    }

    private async Task<Entity<GuildExchangeComponent>> PoweredExchange()
    {
        await SpawnTarget("GuildExchangeTerminal");
        ToggleNeedPower();
        await Server.WaitPost(() => Exchange.StartMarket(Seed));
        await RunTicks(5);
        return (STarget!.Value, SEntMan.GetComponent<GuildExchangeComponent>(STarget.Value));
    }

    private async Task<EntityUid> MakeStation()
    {
        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            var map = ProtoMan.Index<GameMapPrototype>("Empty");
            station = SEntMan.System<StationSystem>().InitializeNewStation(map.Stations["Empty"], new[] { MapData.Grid.Owner }, "Exchange test");
        });
        await RunTicks(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<StationBankAccountComponent>(station)));
        return station;
    }
}
