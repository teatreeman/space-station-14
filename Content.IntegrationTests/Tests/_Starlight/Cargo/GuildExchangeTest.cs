#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Starlight.Cargo.Exchange;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Cargo.Exchange;
using Content.Shared.Cargo.Components;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Stacks;
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

    private List<ExchangeCompanyPrototype> Companies()
    {
        return ProtoMan.EnumeratePrototypes<ExchangeCompanyPrototype>().ToList();
    }

    [Test]
    public async Task PricesRepeatFromASeed()
    {
        await Server.WaitAssertion(() =>
        {
            var settings = Exchange.Settings;
            var a = new ExchangeMarket(Companies(), Seed);
            var b = new ExchangeMarket(Companies(), Seed);
            var c = new ExchangeMarket(Companies(), Seed + 1);
            for (var i = 0; i < 60; i++)
            {
                a.Tick(settings);
                b.Tick(settings);
                c.Tick(settings);
            }

            Assert.That(a.Companies.Count, Is.EqualTo(8));
            foreach (var (id, company) in a.Companies)
            {
                Assert.That(company.History, Is.EqualTo(b.Companies[id].History), $"{id} replays");
            }

            Assert.That(a.Companies.Values.Select(x => x.Price), Is.Not.EqualTo(c.Companies.Values.Select(x => x.Price)));
        });
    }

    /// <summary>
    /// The exchange's own arbitrage test: over many simulated rounds with symmetric news, holding shares and trading at
    /// random both lose money on average once the spread and fee are paid.
    /// </summary>
    [Test]
    public async Task NaiveTradingLosesOnAverage()
    {
        await Server.WaitAssertion(() =>
        {
            var settings = Exchange.Settings;
            long hold = 0, random = 0;
            const int rounds = 200;
            for (var round = 0; round < rounds; round++)
            {
                var market = new ExchangeMarket(Companies(), round);
                var dice = new System.Random(round);
                var bought = new Dictionary<string, (int Shares, int Cost)>();
                var traderShares = new Dictionary<string, int>();

                foreach (var (id, company) in market.Companies)
                {
                    var (cost, price) = ExchangeMath.Buy(company.Price, 20, company.Proto.Liquidity, settings.Spread, settings.Fee);
                    company.Price = price;
                    bought[id] = (20, cost);
                }

                for (var tick = 0; tick < 120; tick++)
                {
                    // Sector news: a random company moves up or down by its news size.
                    if (tick % 6 == 0)
                    {
                        var news = market.Companies.Values.ElementAt(dice.Next(market.Companies.Count));
                        market.QueueShock(news.Proto.ID, dice.Next(2) == 0 ? news.Proto.NewsShock : -news.Proto.NewsShock);
                    }

                    market.Tick(settings);

                    var pick = market.Companies.Values.ElementAt(dice.Next(market.Companies.Count));
                    var held = traderShares.GetValueOrDefault(pick.Proto.ID);
                    if (dice.Next(2) == 0)
                    {
                        var (cost, price) = ExchangeMath.Buy(pick.Price, 5, pick.Proto.Liquidity, settings.Spread, settings.Fee);
                        pick.Price = price;
                        random -= cost;
                        traderShares[pick.Proto.ID] = held + 5;
                    }
                    else if (held > 0)
                    {
                        var (proceeds, price) = ExchangeMath.Sell(pick.Price, held, pick.Proto.Liquidity, settings.Spread, settings.Fee);
                        pick.Price = price;
                        random += proceeds;
                        traderShares[pick.Proto.ID] = 0;
                    }
                }

                foreach (var (id, company) in market.Companies)
                {
                    hold += ExchangeMath.Sell(company.Price, bought[id].Shares, company.Proto.Liquidity, settings.Spread, settings.Fee).Proceeds
                        - bought[id].Cost;
                    if (traderShares.GetValueOrDefault(id) is var left and > 0)
                        random += ExchangeMath.Sell(company.Price, left, company.Proto.Liquidity, settings.Spread, settings.Fee).Proceeds;
                }
            }

            Assert.That(hold / (double) rounds, Is.LessThan(0), "buy and hold");
            Assert.That(random / (double) rounds, Is.LessThan(0), "random trading");
        });
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
            Assert.That(Exchange.GetCargoAccount(machine, out _)!.Holdings["DonkCo"].Shares, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task CargoCapAndCooldownHold()
    {
        var machine = await PoweredExchange();
        var station = await MakeStation();
        await PlaceInHands("QuartermasterIDCard");

        await Server.WaitAssertion(() =>
        {
            var cargo = SEntMan.System<CargoSystem>();
            var balance = cargo.GetBalanceFromAccount(station, "Cargo");
            var company = Exchange.Market!.Companies["NanoTrasen"];
            var tooMany = ExchangeMath.MaxAffordable(company.Price, balance, company.Proto.Liquidity, Exchange.Settings.Spread, Exchange.Settings.Fee);
            Assert.That(tooMany, Is.GreaterThan(0));
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", tooMany),
                Is.EqualTo(Loc.GetString("guild-exchange-cap")));

            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", 1), Is.Null);
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", 1),
                Is.EqualTo(Loc.GetString("guild-exchange-cooldown")));
        });
    }

    [Test]
    public async Task CargoStaffStayCappedAndCannotUnlock()
    {
        var machine = await PoweredExchange();
        var station = await MakeStation();
        await PlaceInHands("CargoIDCard");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Exchange.SetFullBudget(machine, SPlayer, true), Is.EqualTo(Loc.GetString("guild-exchange-full-budget-denied")));
            Assert.That(machine.Comp.FullBudgetBy, Is.Null);
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "NanoTrasen", OverCap(station)),
                Is.EqualTo(Loc.GetString("guild-exchange-cap")));
        });
    }

    [Test]
    public async Task QuartermasterCanUnlockTheWholeBudget()
    {
        var machine = await PoweredExchange();
        var station = await MakeStation();
        await PlaceInHands("QuartermasterIDCard");

        await Server.WaitAssertion(() =>
        {
            var overCap = OverCap(station);
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
    /// A NanoTrasen order that costs more than a quarter of cargo's budget but no more than the whole of it.
    /// </summary>
    private int OverCap(EntityUid station)
    {
        var balance = SEntMan.System<CargoSystem>().GetBalanceFromAccount(station, "Cargo");
        var company = Exchange.Market!.Companies["NanoTrasen"];
        var settings = Exchange.Settings;
        var shares = ExchangeMath.MaxAffordable(company.Price, (int) (balance * 0.4f), company.Proto.Liquidity, settings.Spread, settings.Fee);
        Assert.That(ExchangeMath.Buy(company.Price, shares, company.Proto.Liquidity, settings.Spread, settings.Fee).Cost,
            Is.GreaterThan(balance * settings.CargoCap));
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
    public async Task RoundEndSellsCargoShares()
    {
        var machine = await PoweredExchange();
        var station = await MakeStation();
        await PlaceInHands("QuartermasterIDCard");

        await Server.WaitAssertion(() =>
        {
            var cargo = SEntMan.System<CargoSystem>();
            Assert.That(Exchange.TryTrade(machine, SPlayer, ExchangeAccountKind.Cargo, "MedTak", 5), Is.Null);
            var before = cargo.GetBalanceFromAccount(station, "Cargo");

            var ev = new RoundEndTextAppendEvent();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ev);

            Assert.That(Exchange.GetCargoAccount(machine, out _)!.Holdings, Is.Empty);
            Assert.That(cargo.GetBalanceFromAccount(station, "Cargo"), Is.GreaterThan(before));
            Assert.That(ev.Text, Does.Contain("Guild Exchange"));
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
            Assert.That(vulcan.Value, Is.EqualTo(vulcan.Proto.Price), "the price hasn't moved yet");

            Exchange.Tick();
            Assert.That(vulcan.Value, Is.LessThan(vulcan.Proto.Price * 0.9f));
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
