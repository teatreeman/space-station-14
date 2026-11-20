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

    private async Task ClearHaze()
    {
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<SmokeMachineHazeComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                SEntMan.DeleteEntity(uid);
            }
        });
    }

    /// <summary>
    /// On open floor a puff covers exactly its diamond, spreading a ring every tenth of a second, so even the biggest
    /// one is done within a second and a half.
    /// </summary>
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task PuffFillsItsDiamondQuickly(int setting)
    {
        await Placed();
        await OpenFloor();
        await Server.WaitAssertion(() =>
        {
            Fill("Water", 100);
            Machine.SetSetting(STarget!.Value, setting);
            Assert.That(Machine.TryPuff(STarget.Value));
        });

        await RunSeconds(0.5f);
        var halfway = 0;
        await Server.WaitPost(() => halfway = HazeCount());

        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(halfway, Is.GreaterThan(1), "it was spreading after half a second");
            Assert.That(HazeCount(), Is.EqualTo(SmokeMachinePuffs.Tiles(setting)), "the whole diamond after a second and a half");
        });

        await RunSeconds(2f);
        await Server.WaitAssertion(() =>
            Assert.That(HazeCount(), Is.EqualTo(SmokeMachinePuffs.Tiles(setting)), "and no further"));
        await ClearHaze();
    }

    [Test]
    public async Task HazeTouchesThingsLyingInIt()
    {
        await Placed();
        EntityUid cube = default;
        await Server.WaitAssertion(() =>
        {
            cube = SEntMan.SpawnAtPosition("MonkeyCube", SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates);
            Fill("Water", 100);
            Assert.That(Machine.TryPuff(STarget.Value));
        });

        await RunSeconds(1f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(cube), "not touched before the first two seconds"));

        await RunSeconds(2f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(cube), Is.False, "the haze's water got to the cube"));
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

    [Test]
    public async Task FloorsGetNoMoreThanTheTankLost()
    {
        await Placed();

        FixedPoint2 lost = 0;
        await Server.WaitAssertion(() =>
        {
            // Glue spills like lube but doesn't evaporate, so the puddles can be weighed.
            Fill("SpaceGlue", 100);
            Machine.SetSetting(STarget!.Value, 3);
            Assert.That(Machine.TryPuff(STarget.Value));
            lost = 100 - Tank().Volume;
            Assert.That(lost, Is.EqualTo(FixedPoint2.New(9.05f)));
        });

        await RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(HazeCount(), Is.GreaterThan(1), "the haze spread");
            Assert.That(PuddleVolume("SpaceGlue"), Is.GreaterThan(FixedPoint2.Zero), "the glue still wets the floor");
            Assert.That(PuddleVolume("SpaceGlue"), Is.LessThanOrEqualTo(lost), "glue puddles hold no more than what left the tank");
        });
    }

    /// <summary>
    /// However many tiles end up in one puff, their floors share no more than the tank lost: here 40 extra tiles join
    /// a 25-tile puff.
    /// </summary>
    [Test]
    public async Task PuffFloorsStayWithinItsBudget()
    {
        await Placed();

        FixedPoint2 lost = 0;
        await Server.WaitAssertion(() =>
        {
            Fill("SpaceGlue", 100);
            Assert.That(Machine.TryPuff(STarget!.Value));
            lost = 100 - Tank().Volume;

            var query = SEntMan.EntityQueryEnumerator<SmokeMachineHazeComponent>();
            Assert.That(query.MoveNext(out var origin, out var originHaze));

            var coords = SEntMan.GetComponent<TransformComponent>(STarget.Value).Coordinates;
            for (var i = 0; i < 40; i++)
            {
                var extra = SEntMan.SpawnAttachedTo("SmokeMachineHaze", coords);
                Machine.JoinPuff((extra, SEntMan.EnsureComponent<SmokeMachineHazeComponent>(extra)), (origin, originHaze!));
                Assert.That(Solutions.TryGetSolution(extra, SmokeComponent.SolutionName, out var soln, out _));
                Solutions.TryAddReagent(soln!.Value, "SpaceGlue", 20);
            }
        });

        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            FixedPoint2 reacted = 0;
            var query = SEntMan.EntityQueryEnumerator<SmokeMachineHazeComponent>();
            while (query.MoveNext(out _, out var haze))
            {
                reacted += haze.FloorReacted;
            }

            Assert.That(HazeCount(), Is.GreaterThan(SmokeMachinePuffs.Tiles(1)), "more tiles than the puff is meant to cover");
            Assert.That(reacted, Is.GreaterThan(FixedPoint2.Zero), "the floors were wetted");
            Assert.That(reacted, Is.LessThanOrEqualTo(lost), "all the puff's floors together got no more than the tank lost");
        });
    }

    [Test]
    public async Task DrinkOverPuddlesAddsNoMoreThanTheTankLost()
    {
        await Placed();

        FixedPoint2 lost = 0;
        FixedPoint2 before = 0;
        await Server.WaitAssertion(() =>
        {
            // Puddles of water around the machine for the drink to join.
            var puddles = SEntMan.System<SharedPuddleSystem>();
            var coords = SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates;
            foreach (var (x, y) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                puddles.TrySpillAt(coords.Offset(new Vector2(x, y)), new Solution("Water", FixedPoint2.New(20)), out _, sound: false);
            }

            Fill("JuiceOrange", 100);
            Machine.SetSetting(STarget.Value, 3);
            before = PuddleVolume("JuiceOrange");
            Assert.That(Machine.TryPuff(STarget.Value));
            lost = 100 - Tank().Volume;
        });

        await RunSeconds(5);
        await Server.WaitAssertion(() =>
            Assert.That(PuddleVolume("JuiceOrange") - before, Is.LessThanOrEqualTo(lost), "juice added to puddles is no more than what left the tank"));
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
