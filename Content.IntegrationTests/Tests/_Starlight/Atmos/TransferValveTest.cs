#nullable enable
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Starlight.Atmos.TransferValve;
using Content.Shared._Starlight.Atmos.TransferValve;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;

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
            Assert.That(Air(one).TotalMoles, Is.GreaterThan(0f));
            Assert.That(Air(two).Volume, Is.EqualTo(volumeTwo).Within(0.01f));
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

        await Server.WaitAssertion(() =>
        {
            // A sensible bomb: some very hot plasma and an overfilled oxygen tank.
            Air(one).Multiply(0.6f);
            Air(one).Temperature = 1500f;
            Air(two).Multiply(2f);
            Assert.That(SEntMan.System<TransferValveSystem>().SetOpen(valve, true));
        });

        await RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(two), "The second tank should have burst"));
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
    public async Task TanksGoInByHand()
    {
        await SpawnTarget("TransferValve");
        await InteractUsing("PlasmaTankFilled");
        await InteractUsing("OxygenTankFilled");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Slots().GetItemOrNull(STarget!.Value, "tank_one"), Is.Not.Null);
            Assert.That(Slots().GetItemOrNull(STarget!.Value, "tank_two"), Is.Not.Null);
        });
    }

    [Test]
    public async Task ValveGoesOntoATank()
    {
        await SpawnTarget("OxygenTankFilled");
        var valve = await PlaceInHands("TransferValve");
        await Interact();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Slots().GetItemOrNull(SEntMan.GetEntity(valve), "tank_one"), Is.EqualTo(STarget));
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
        await InteractUsing("CableApcStack1", 15);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Valve(STarget!.Value).Wired);
            Assert.That(SEntMan.GetComponent<ClothingComponent>(STarget!.Value).Slots, Is.EqualTo(SlotFlags.BACK));
        });

        await InteractUsing("Wirecutter");
        await Server.WaitAssertion(() =>
        {
            Assert.That(Valve(STarget!.Value).Wired, Is.False);
            Assert.That(SEntMan.GetComponent<ClothingComponent>(STarget!.Value).Slots, Is.EqualTo(SlotFlags.NONE));
        });
    }

    [Test]
    public async Task DeletedTankClosesIt()
    {
        var (valve, one, two) = await SpawnLoadedValve();
        var system = SEntMan.System<TransferValveSystem>();

        await Server.WaitAssertion(() =>
        {
            var volumeTwo = Air(two).Volume;
            Assert.That(system.SetOpen(valve, true));

            SEntMan.DeleteEntity(one);
            Assert.That(Valve(valve).Open, Is.False);
            Assert.That(Air(two).Volume, Is.EqualTo(volumeTwo).Within(0.01f));
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

    private void Trigger(EntityUid valve)
    {
        var ev = new TriggerEvent(Key: TriggerSystem.DefaultTriggerKey);
        SEntMan.EventBus.RaiseLocalEvent(valve, ref ev);
    }

    private ItemSlotsSystem Slots() => SEntMan.System<ItemSlotsSystem>();

    private TransferValveComponent Valve(EntityUid uid) => SEntMan.GetComponent<TransferValveComponent>(uid);

    private GasMixture Air(EntityUid tank) => SEntMan.GetComponent<GasTankComponent>(tank).Air;
}
