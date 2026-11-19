#nullable enable
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Starlight.Atmos.TransferValve;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Trigger;
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
    public async Task TriggerOpensIt()
    {
        var (valve, _, _) = await SpawnLoadedValve();

        await Server.WaitAssertion(() =>
        {
            var ev = new TriggerEvent();
            SEntMan.EventBus.RaiseLocalEvent(valve, ref ev);
            Assert.That(SEntMan.GetComponent<TransferValveComponent>(valve).Open);
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
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.GetItemOrNull(STarget!.Value, "tank_one"), Is.Not.Null);
            Assert.That(slots.GetItemOrNull(STarget!.Value, "tank_two"), Is.Not.Null);
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
            Assert.That(SEntMan.GetComponent<TransferValveComponent>(valve).Open, Is.False);
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
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryInsert(valve, "tank_one", one, null));
            Assert.That(slots.TryInsert(valve, "tank_two", two, null));
        });
        return (valve, one, two);
    }

    private GasMixture Air(EntityUid tank) => SEntMan.GetComponent<GasTankComponent>(tank).Air;
}
