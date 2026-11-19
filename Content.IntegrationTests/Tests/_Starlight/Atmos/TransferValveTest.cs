#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Starlight.Atmos.TransferValve;
using Content.Shared._Starlight.Atmos.TransferValve;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Content.Shared.Verbs;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Starlight.Atmos;

[TestFixture]
public sealed class TransferValveTest : InteractionTest
{
    [Test]
    public async Task OpenAndClose()
    {
        var (valve, one, two) = await SpawnLoadedValve();
        var system = SEntMan.System<TransferValveSystem>();

        await Server.WaitAssertion(() =>
        {
            var total = Air(one).TotalMoles + Air(two).TotalMoles;
            var volumeOne = Air(one).Volume;
            var volumeTwo = Air(two).Volume;

            Assert.That(system.SetOpen(valve, true));
            Assert.That(Air(one).TotalMoles, Is.EqualTo(0f).Within(0.01f));
            Assert.That(Air(two).TotalMoles, Is.EqualTo(total).Within(total * 0.01f));
            Assert.That(Air(two).Volume, Is.EqualTo(volumeOne + volumeTwo).Within(0.01f));

            Assert.That(system.SetOpen(valve, false));
            var share = volumeOne / (volumeOne + volumeTwo);
            Assert.That(Air(one).TotalMoles, Is.EqualTo(total * share).Within(total * 0.01f));
            Assert.That(Air(two).TotalMoles, Is.EqualTo(total * (1 - share)).Within(total * 0.01f));
            Assert.That(Air(two).Volume, Is.EqualTo(volumeTwo).Within(0.01f));
        });
    }

    [Test]
    public async Task EjectingATankSplitsTheGas()
    {
        var (valve, one, two) = await SpawnLoadedValve();

        await Server.WaitAssertion(() =>
        {
            var total = Air(one).TotalMoles + Air(two).TotalMoles;
            var volumeTwo = Air(two).Volume;
            Assert.That(SEntMan.System<TransferValveSystem>().SetOpen(valve, true));

            Assert.That(Slots().TryEject(valve, "tank_one", null, out _));
            Assert.That(Valve(valve).Open, Is.False);
            Assert.That(Air(one).TotalMoles + Air(two).TotalMoles, Is.EqualTo(total).Within(total * 0.01f));
            Assert.That(Air(one).TotalMoles, Is.GreaterThan(0f));
            Assert.That(Air(two).Volume, Is.EqualTo(volumeTwo).Within(0.01f));
        });
    }

    [Test]
    public async Task LookingAtTheMenuLeavesItOpen()
    {
        var (valve, _, _) = await SpawnLoadedValve();

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<TransferValveSystem>().SetOpen(valve, true));
            SEntMan.System<SharedVerbSystem>().GetLocalVerbs(valve, SPlayer, typeof(AlternativeVerb));
            SEntMan.System<SharedVerbSystem>().GetLocalVerbs(valve, SPlayer, typeof(Verb));
            Assert.That(Valve(valve).Open, "Opening the context menu shouldn't touch the valve");
        });
    }

    [Test]
    public async Task OnlyWorkedWhileHeld()
    {
        var (valve, _, _) = await SpawnLoadedValve();

        await Server.WaitAssertion(() =>
        {
            var verbs = SEntMan.System<SharedVerbSystem>().GetLocalVerbs(valve, SPlayer, typeof(Verb));
            Assert.That(verbs.Any(v => v.Text == Loc.GetString("transfer-valve-verb-open")), Is.False, "Not from the floor");
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(SPlayer, valve));
            var verbs = SEntMan.System<SharedVerbSystem>().GetLocalVerbs(valve, SPlayer, typeof(Verb));
            Assert.That(verbs.Any(v => v.Text == Loc.GetString("transfer-valve-verb-open")), "Held, it can be opened");
        });
    }

    [Test]
    public async Task TriggerTogglesIt()
    {
        var (valve, _, _) = await SpawnLoadedValve();

        await Server.WaitAssertion(() =>
        {
            Trigger(valve);
            Assert.That(Valve(valve).Open);

            // A second trigger straight away is ignored.
            Trigger(valve);
            Assert.That(Valve(valve).Open);
        });

        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Trigger(valve);
            Assert.That(Valve(valve).Open, Is.False);
        });
    }

    [Test]
    public async Task TimerStartingDoesntOpenIt()
    {
        var (valve, _, _) = await SpawnLoadedValve();

        await Server.WaitAssertion(() =>
        {
            var ev = new TriggerEvent(Key: "startTimer");
            SEntMan.EventBus.RaiseLocalEvent(valve, ref ev);
            Assert.That(Valve(valve).Open, Is.False);
        });
    }

    [Test]
    public async Task TimerOpensItAfterItsDelay()
    {
        var (valve, _, _) = await SpawnLoadedValve();
        var timer = await SpawnEntity("TimerTrigger", SEntMan.GetCoordinates(PlayerCoords));

        await Server.WaitAssertion(() =>
        {
            Assert.That(Slots().TryInsert(valve, "payloadTrigger", timer, null));
            var ev = new TriggerEvent(Key: "startTimer");
            SEntMan.EventBus.RaiseLocalEvent(valve, ref ev);
            Assert.That(Valve(valve).Open, Is.False);
        });

        await RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(Valve(valve).Open, "The timer should open it once it runs out"));
    }

    [Test]
    public async Task HotMixExplodes()
    {
        var (valve, one, two) = await SpawnLoadedValve();
        var table = await SpawnEntity("TableWood", SEntMan.GetCoordinates(PlayerCoords).Offset(new System.Numerics.Vector2(1, 0)));

        await Server.WaitAssertion(() =>
        {
            // A sensible bomb: some very hot plasma and an overfilled oxygen tank.
            Air(one).Multiply(0.6f);
            Air(one).Temperature = 1500f;
            Air(two).Multiply(2f);
            Assert.That(SEntMan.System<TransferValveSystem>().SetOpen(valve, true));
        });

        await RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(two), "The second tank should have burst");
            Assert.That(SEntMan.Deleted(valve), "and taken the valve with it");
            // A plain rupture also deletes the tank, so check that the blast actually hit something.
            Assert.That(SEntMan.Deleted(table) || SEntMan.System<DamageableSystem>().GetTotalDamage(table) > 0,
                "The table next to it should have been caught in an explosion");
        });
    }

    [Test]
    public async Task TanksShowTheirKind()
    {
        var (valve, _, _) = await SpawnLoadedValve();

        await Server.WaitAssertion(() =>
        {
            var appearance = SEntMan.System<SharedAppearanceSystem>();
            Assert.That(appearance.TryGetData(valve, TransferValveVisuals.TankOne, out string? one));
            Assert.That(appearance.TryGetData(valve, TransferValveVisuals.TankTwo, out string? two));
            Assert.That(one, Is.EqualTo("plasma"));
            Assert.That(two, Is.EqualTo("oxygen"));
        });
    }

    [Test]
    [TestCase("JetpackBlueFilled", "jetpack")]
    [TestCase("NitrogenTankFilled", "generic")]
    public async Task OtherTanksShowTheirKind(string tank, string state)
    {
        var coords = SEntMan.GetCoordinates(PlayerCoords);
        var valve = await SpawnEntity("TransferValve", coords);
        var item = await SpawnEntity(tank, coords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Slots().TryInsert(valve, "tank_one", item, null));
            Assert.That(SEntMan.System<SharedAppearanceSystem>().TryGetData(valve, TransferValveVisuals.TankOne, out string? shown));
            Assert.That(shown, Is.EqualTo(state));
        });
    }

    [Test]
    public async Task TimerArmedByHand()
    {
        var (valve, _, _) = await SpawnLoadedValve();
        var timer = await SpawnEntity("TimerTrigger", SEntMan.GetCoordinates(PlayerCoords));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Slots().TryInsert(valve, "payloadTrigger", timer, null));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(SPlayer, valve));
        });

        await UseInHand();
        await Server.WaitAssertion(() => Assert.That(Valve(valve).Open, Is.False, "Not until the timer runs out"));

        await RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(Valve(valve).Open));
    }

    [Test]
    public async Task TanksGoInByHand()
    {
        await SpawnTarget("TransferValve");
        await InteractUsing("PlasmaTankFilled");
        await InteractUsing("OxygenTankFilled");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Prototype(Slots().GetItemOrNull(STarget!.Value, "tank_one")), Is.EqualTo("PlasmaTankFilled"));
            Assert.That(Prototype(Slots().GetItemOrNull(STarget!.Value, "tank_two")), Is.EqualTo("OxygenTankFilled"));
        });
    }

    [Test]
    public async Task MagbootsDontFit()
    {
        var coords = SEntMan.GetCoordinates(PlayerCoords);
        var valve = await SpawnEntity("TransferValve", coords);
        var boots = await SpawnEntity("ClothingShoesBootsMagSyndie", coords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<GasTankComponent>(boots));
            Assert.That(Slots().TryInsert(valve, "tank_one", boots, null), Is.False);
        });
    }

    [Test]
    public async Task CableStraps()
    {
        await SpawnTarget("TransferValve");
        var cable = await PlaceInHands("CableApcStack1", 20);
        await Interact();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Valve(STarget!.Value).Wired);
            Assert.That(SEntMan.GetComponent<ClothingComponent>(STarget!.Value).Slots, Is.EqualTo(SlotFlags.BACK));
            Assert.That(SEntMan.GetComponent<StackComponent>(SEntMan.GetEntity(cable)).Count, Is.EqualTo(5), "Fifteen lengths used");
        });

        await InteractUsing("Wirecutter");
        await Server.WaitAssertion(() =>
        {
            Assert.That(Valve(STarget!.Value).Wired, Is.False);
            Assert.That(SEntMan.GetComponent<ClothingComponent>(STarget!.Value).Slots, Is.EqualTo(SlotFlags.NONE));
            var cableBack = SEntMan.System<EntityLookupSystem>().GetEntitiesInRange<StackComponent>(SEntMan.GetCoordinates(TargetCoords), 0.5f);
            Assert.That(cableBack.Any(s => s.Comp.StackTypeId == "Cable" && s.Comp.Count == 15), "Cutting gives the cable back");
        });
    }

    [Test]
    public async Task NotEnoughCable()
    {
        await SpawnTarget("TransferValve");
        var cable = await PlaceInHands("CableApcStack1", 10);
        await Interact();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Valve(STarget!.Value).Wired, Is.False);
            Assert.That(SEntMan.GetComponent<StackComponent>(SEntMan.GetEntity(cable)).Count, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task BurstTankClosesIt()
    {
        var (valve, one, two) = await SpawnLoadedValve();
        var system = SEntMan.System<TransferValveSystem>();

        await Server.WaitAssertion(() =>
        {
            Assert.That(system.SetOpen(valve, true));
            var moles = Air(two).TotalMoles;
            var volume = Air(two).Volume;

            SEntMan.DeleteEntity(one);
            Assert.That(Valve(valve).Open, Is.False);
            Assert.That(Air(two).TotalMoles, Is.EqualTo(moles).Within(0.01f), "The other tank keeps its gas");
            Assert.That(Air(two).Volume, Is.EqualTo(volume).Within(0.01f), "and its pressure doesn't jump");
        });
    }

    private async Task<(EntityUid Valve, EntityUid One, EntityUid Two)> SpawnLoadedValve()
    {
        var coords = SEntMan.GetCoordinates(PlayerCoords);
        var valve = await SpawnEntity("TransferValve", coords);
        var one = await SpawnEntity("PlasmaTankFilled", coords);
        var two = await SpawnEntity("OxygenTankFilled", coords);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Slots().TryInsert(valve, "tank_one", one, null));
            Assert.That(Slots().TryInsert(valve, "tank_two", two, null));
        });
        return (valve, one, two);
    }

    private string? Prototype(EntityUid? uid) => uid == null ? null : SEntMan.GetComponent<MetaDataComponent>(uid.Value).EntityPrototype?.ID;

    private void Trigger(EntityUid valve)
    {
        var ev = new TriggerEvent(Key: TriggerSystem.DefaultTriggerKey);
        SEntMan.EventBus.RaiseLocalEvent(valve, ref ev);
    }

    private ItemSlotsSystem Slots() => SEntMan.System<ItemSlotsSystem>();

    private TransferValveComponent Valve(EntityUid uid) => SEntMan.GetComponent<TransferValveComponent>(uid);

    private GasMixture Air(EntityUid tank) => SEntMan.GetComponent<GasTankComponent>(tank).Air;
}
