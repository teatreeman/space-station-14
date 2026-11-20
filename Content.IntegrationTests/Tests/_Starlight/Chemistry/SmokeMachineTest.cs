#nullable enable
using System.Numerics;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Starlight.Chemistry.Components;
using Content.Server._Starlight.Chemistry.EntitySystems;
using Content.Server._Starlight.Plumbing;
using Content.Shared._Starlight.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Maps;
using Content.Shared.Wires;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Damage.Systems;
using Content.Server.Fluids.EntitySystems;
using System.Collections.Generic;
using Content.Shared._Starlight.Construction;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Chemistry;

[TestFixture]
public sealed class SmokeMachineTest : InteractionTest
{
    private SharedSolutionContainerSystem Solutions => SEntMan.System<SharedSolutionContainerSystem>();
    private SmokeMachineSystem Machine => SEntMan.System<SmokeMachineSystem>();

    private Solution Tank()
    {
        Assert.That(Solutions.TryGetSolution(STarget!.Value, "tank", out _, out var tank), "the machine has a tank");
        return tank!;
    }

    private void Fill(string reagent, FixedPoint2 amount)
    {
        Assert.That(Solutions.TryGetSolution(STarget!.Value, "tank", out var soln, out _));
        Assert.That(Solutions.TryAddReagent(soln!.Value, reagent, amount), $"{amount}u of {reagent} fits");
    }

    private async Task Placed()
    {
        await SpawnTarget("SmokeMachine");
        ToggleNeedPower();
        await RunTicks(5);
    }

    private FixedPoint2 PuddleVolume(string reagent)
    {
        FixedPoint2 total = 0;
        var query = SEntMan.EntityQueryEnumerator<PuddleComponent>();
        while (query.MoveNext(out var uid, out var puddle))
        {
            if (Solutions.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
                total += solution.GetTotalPrototypeQuantity(reagent);
        }

        return total;
    }

    private int HazeCount()
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<SmokeMachineHazeComponent>();
        while (query.MoveNext(out _, out _))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Lays an open floor around the machine, wide enough for the biggest puff.
    /// </summary>
    private async Task OpenFloor()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var xform = SEntMan.GetComponent<TransformComponent>(STarget!.Value);
            var gridUid = xform.GridUid!.Value;
            var grid = SEntMan.GetComponent<MapGridComponent>(gridUid);
            var center = map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var plating = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                {
                    map.SetTile(gridUid, grid, center + new Vector2i(x, y), plating);
                }
            }
        });
    }

    /// <summary>
    /// Spawns a human next to the machine, wearing whatever's given on its face, and switches the machine on.
    /// </summary>
    private async Task<EntityUid> HumanInHaze(string reagent, string? mask = null, bool internals = false, int offset = 1)
    {
        EntityUid human = default;
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates;
            human = SEntMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(offset, 0)));
            if (mask != null)
            {
                var worn = SEntMan.SpawnAtPosition(mask, coords);
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(human, worn, "mask", silent: true, force: true));
                if (internals)
                {
                    var tank = SEntMan.SpawnAtPosition("OxygenTankFilled", coords);
                    Assert.That(SEntMan.System<InventorySystem>().TryEquip(human, tank, "belt", silent: true, force: true));
                    var system = SEntMan.System<InternalsSystem>();
                    var comp = SEntMan.GetComponent<InternalsComponent>(human);
                    system.ConnectBreathTool((human, comp), worn);
                    Assert.That(system.TryConnectTank((human, comp), tank));
                    Assert.That(system.AreInternalsWorking(human), "internals on");
                }
            }
        });

        await Server.WaitPost(() =>
        {
            if (Tank().Volume <= 0)
                Fill(reagent, 100);
            Machine.SetEnabled(STarget!.Value, true);
        });
        return human;
    }

    private float Caustic(EntityUid uid)
    {
        return SEntMan.System<DamageableSystem>().GetAllDamage(uid).DamageDict.GetValueOrDefault("Caustic").Float();
    }

    /// <summary>
    /// Someone standing in a running machine's haze breathes its chems into their blood, like with a smoke grenade.
    /// </summary>
    [Test]
    public async Task PeopleInTheHazeBreatheItIn()
    {
        await Placed();
        await OpenFloor();
        var human = await HumanInHaze("SpaceDrugs");

        await RunSeconds(8);
        await Server.WaitAssertion(() => Assert.That(BloodHas(human, "SpaceDrugs"), "the haze got into their blood"));
    }

    /// <summary>
    /// A gas-tight mask or working internals keeps the haze off entirely: nothing breathed in (space drugs in the blood)
    /// and nothing touched (acid burns). A mask that isn't gas-tight does nothing.
    /// </summary>
    [TestCase("SpaceDrugs", null, false, true)]
    [TestCase("SpaceDrugs", "ClothingMaskSterile", false, true)]
    [TestCase("SpaceDrugs", "ClothingMaskGas", false, false)]
    [TestCase("SpaceDrugs", "ClothingMaskBreath", false, false)]
    [TestCase("SpaceDrugs", "ClothingMaskBreath", true, false)]
    [TestCase("FluorosulfuricAcid", null, false, true)]
    [TestCase("FluorosulfuricAcid", "ClothingMaskSterile", false, true)]
    [TestCase("FluorosulfuricAcid", "ClothingMaskGas", false, false)]
    [TestCase("FluorosulfuricAcid", "ClothingMaskBreath", false, false)]
    [TestCase("FluorosulfuricAcid", "ClothingMaskBreath", true, false)]
    public async Task GasTightMasksAndInternalsKeepTheHazeOff(string reagent, string? mask, bool internals, bool reaches)
    {
        await Placed();
        await OpenFloor();
        var human = await HumanInHaze(reagent, mask, internals);

        await RunSeconds(8);
        await Server.WaitAssertion(() =>
        {
            if (reagent == "SpaceDrugs")
                Assert.That(BloodHas(human, reagent), Is.EqualTo(reaches), "breathed in");
            else
                Assert.That(Caustic(human) > 0, Is.EqualTo(reaches), "touched");
        });
    }

    /// <summary>
    /// The protection is the machine's own: other smoke still reaches a masked face the fork's usual way.
    /// </summary>
    [Test]
    public async Task OtherSmokeStillTouchesMaskedPeople()
    {
        await Placed();
        await OpenFloor();
        EntityUid human = default;
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates;
            human = SEntMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(1, 0)));
            var worn = SEntMan.SpawnAtPosition("ClothingMaskGas", coords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(human, worn, "mask", silent: true, force: true));
            var smoke = SEntMan.SpawnAtPosition("Smoke", coords.Offset(new Vector2(1, 0)));
            SEntMan.System<SmokeSystem>().StartSmoke(smoke, new Solution("FluorosulfuricAcid", 20), 20, 0);
        });

        await RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(Caustic(human), Is.GreaterThan(0f), "grenade-style smoke is unchanged"));
    }

    [Test]
    public async Task HazeSpreadsLikeStationSmoke()
    {
        await Placed();
        await OpenFloor();
        await Server.WaitAssertion(() =>
        {
            Fill("Water", 100);
            Assert.That(Machine.TryPuff(STarget!.Value));
            Assert.That(HazeCount(), Is.EqualTo(1));
        });

        await RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(HazeCount(), Is.GreaterThan(5), "it spreads through the station's smoke spreading"));
    }

    private bool BloodHas(EntityUid uid, string reagent)
    {
        var blood = SEntMan.GetComponent<BloodstreamComponent>(uid);
        return Solutions.TryGetSolution(uid, blood.BloodSolutionName, out _, out var solution) &&
               solution.GetTotalPrototypeQuantity(reagent) > 0;
    }

    [Test]
    public async Task FoamDoesNotHoldBackAPuff()
    {
        await Placed();
        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnAtPosition("Foam", SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates);
            Fill("Water", 100);
            Assert.That(Machine.TryPuff(STarget.Value), "foam on its tile isn't smoke");
        });
    }

    [Test]
    public void PuffSizes()
    {
        Assert.That(SmokeMachinePuffs.Tiles(1), Is.EqualTo(25));
        Assert.That(SmokeMachinePuffs.Tiles(2), Is.EqualTo(85));
        Assert.That(SmokeMachinePuffs.Tiles(3), Is.EqualTo(181));
    }

    [Test]
    public async Task PuffsButNotWhileSmokeSitsOnItsTile()
    {
        await Placed();
        await Server.WaitAssertion(() =>
        {
            Fill("Water", 100);
            Assert.That(Machine.TryPuff(STarget!.Value), "a filled, powered, anchored machine puffs");
            Assert.That(Tank().Volume, Is.EqualTo(FixedPoint2.New(100 - 25 / 20f)), "setting 1 costs 1.25u");
            Assert.That(HazeCount(), Is.EqualTo(1));

            Assert.That(Machine.TryPuff(STarget.Value), Is.False, "no new puff while smoke sits on its tile");
            Assert.That(Tank().Volume, Is.EqualTo(FixedPoint2.New(100 - 25 / 20f)), "a skipped step costs nothing");
        });
    }

    [Test]
    public async Task PuffCarriesTwentyUnitsButCostsOnlyItsTiles()
    {
        await Placed();
        await Server.WaitAssertion(() =>
        {
            Fill("Water", 200);
            Assert.That(Machine.TryPuff(STarget!.Value));
            Assert.That(Tank().Volume, Is.EqualTo(FixedPoint2.New(200 - 25 / 20f)), "the tank pays 25 tiles / 20");

            var query = SEntMan.EntityQueryEnumerator<SmokeMachineHazeComponent, SmokeComponent>();
            Assert.That(query.MoveNext(out var haze, out _, out var smoke));
            Assert.That(Solutions.TryGetSolution(haze, SmokeComponent.SolutionName, out _, out var carried));
            Assert.That(carried!.Volume, Is.EqualTo(FixedPoint2.New(20)), "a puff carries a 20u copy, not the whole tank");
            Assert.That(smoke.TransferRate, Is.EqualTo(FixedPoint2.New(1)), "20u breathed out over its 20 seconds");
        });
    }

    /// <summary>
    /// The haze only works through the air: a tank of glue leaves nothing on the floor, and juice doesn't join puddles.
    /// </summary>
    [TestCase("SpaceGlue", false)]
    [TestCase("JuiceOrange", true)]
    public async Task HazeNeverWetsTheFloor(string reagent, bool puddlesAround)
    {
        await Placed();
        await OpenFloor();

        FixedPoint2 before = 0;
        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates;
            if (puddlesAround)
            {
                foreach (var (x, y) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    SEntMan.System<SharedPuddleSystem>().TrySpillAt(coords.Offset(new Vector2(x, y)), new Solution("Water", FixedPoint2.New(20)), out _, sound: false);
                }
            }

            Fill(reagent, 100);
            Machine.SetSetting(STarget.Value, 3);
            before = PuddleVolume(reagent);
            Assert.That(Machine.TryPuff(STarget.Value));
            Assert.That(100 - Tank().Volume, Is.EqualTo(FixedPoint2.New(9.05f)));
        });

        await RunSeconds(8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(HazeCount(), Is.GreaterThan(1), "the haze spread");
            Assert.That(PuddleVolume(reagent), Is.EqualTo(before), "nothing of it reached the floor");
        });
    }

    [Test]
    public async Task SettingsFromItsWindow()
    {
        await Placed();
        await Interact();

        await SendBui(SmokeMachineUiKey.Key, new SmokeMachineSetSettingMessage(3));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SmokeMachineComponent>(STarget!.Value).Setting, Is.EqualTo(3));
            Assert.That(Machine.TankLoss(STarget.Value), Is.EqualTo(FixedPoint2.New(9.05f)));
        });

        await SendBui(SmokeMachineUiKey.Key, new SmokeMachineSetSettingMessage(9));
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<SmokeMachineComponent>(STarget!.Value).Setting, Is.EqualTo(3), "settings above the range are clamped"));
    }

    [Test]
    public async Task SwitchingRules()
    {
        await Placed();
        await Interact();

        await SendBui(SmokeMachineUiKey.Key, new SmokeMachineToggleMessage());
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SmokeMachineComponent>(STarget!.Value).Enabled, Is.False, "an empty machine won't switch on");
            Fill("Water", 50);
        });

        await SendBui(SmokeMachineUiKey.Key, new SmokeMachineToggleMessage());
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SmokeMachineComponent>(STarget!.Value).Enabled, "it runs with reagents in");
            Assert.That(Tank().Volume, Is.LessThan(FixedPoint2.New(50)), "running uses the tank");
        });

        await Server.WaitAssertion(() =>
        {
            var panel = SEntMan.GetComponent<WiresPanelComponent>(STarget!.Value);
            var ev = new AttemptChangePanelEvent(!panel.Open, SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, ref ev);
            Assert.That(ev.Cancelled, "no opening or closing the panel while it runs");

            var deconstruct = new ConstructionInteractAttemptEvent(SPlayer, STarget.Value);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, ref deconstruct);
            Assert.That(deconstruct.Canceled, "no taking it apart while it runs");
        });

        await Server.WaitPost(() => SEntMan.System<TransformSystem>().Unanchor(STarget!.Value, SEntMan.GetComponent<TransformComponent>(STarget.Value)));
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<SmokeMachineComponent>(STarget!.Value).Enabled, Is.False, "unbolting it switches it off"));

        await SendBui(SmokeMachineUiKey.Key, new SmokeMachinePurgeMessage());
        await Server.WaitAssertion(() => Assert.That(Tank().Volume, Is.EqualTo(FixedPoint2.Zero), "purge empties the tank"));
    }

    [Test]
    public async Task PlumbingBringsInFiveReagentsAtMost()
    {
        await Placed();
        await Server.WaitAssertion(() =>
        {
            foreach (var reagent in new[] { "Water", "Iron", "Copper", "Silver", "Gold" })
            {
                Fill(reagent, 10);
            }

            Assert.That(Tank().Contents, Has.Count.EqualTo(5));

            var sixth = new PlumbingPullIntoAttemptEvent(STarget!.Value, "Potassium", 10);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, ref sixth);
            Assert.That(sixth.Cancelled, "a sixth reagent is refused");

            var more = new PlumbingPullIntoAttemptEvent(STarget.Value, "Water", 10);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, ref more);
            Assert.That(more.Cancelled, Is.False, "more of one it already holds is fine");
        });
    }
}
