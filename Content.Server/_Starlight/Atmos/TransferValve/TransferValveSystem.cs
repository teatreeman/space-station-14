using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Atmos.TransferValve;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Trigger;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Atmos.TransferValve;

/// <summary>
/// Opens and closes tank transfer valves.
/// </summary>
public sealed partial class TransferValveSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    [SubscribeLocalEvent]
    private void OnTrigger(Entity<TransferValveComponent> ent, ref TriggerEvent args)
    {
        SetOpen(ent.AsNullable(), true, args.User);
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<TransferValveComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetTanks(ent) is null)
            return;

        var user = args.User;
        var open = ent.Comp.Open;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(open ? "transfer-valve-verb-close" : "transfer-valve-verb-open"),
            Priority = 3,
            Act = () => SetOpen(ent.AsNullable(), !open, user),
        });
    }

    /// <summary>
    /// Closes the valve before a tank comes off, so each tank leaves with its share of the gas.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnRemovingAttempt(Entity<TransferValveComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.TankOneSlot || args.Container.ID == ent.Comp.TankTwoSlot)
            SetOpen(ent.AsNullable(), false);
    }

    [SubscribeLocalEvent]
    private void OnInserted(Entity<TransferValveComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateVisuals(ent);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<TransferValveComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // A tank that ruptures and gets deleted skips the removal attempt, so the valve is still open here.
        if (ent.Comp.Open && (args.Container.ID == ent.Comp.TankOneSlot || args.Container.ID == ent.Comp.TankTwoSlot))
        {
            ent.Comp.Open = false;
            if (TryComp<GasTankComponent>(_itemSlots.GetItemOrNull(ent, ent.Comp.TankTwoSlot), out var two))
                two.Air.Volume = ent.Comp.TankTwoVolume;
        }

        UpdateVisuals(ent);
    }

    /// <summary>
    /// Opening pours the first tank into the second; closing splits the gas back by volume. Needs both tanks.
    /// </summary>
    public bool SetOpen(Entity<TransferValveComponent?> ent, bool open, EntityUid? user = null)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Open == open || GetTanks((ent, ent.Comp)) is not var (one, two))
            return false;

        _audio.PlayPvs(ent.Comp.ValveSound, ent);
        if (open)
        {
            ent.Comp.TankTwoVolume = two.Air.Volume;
            // Grow the volume with the gas so the merge alone doesn't spike the pressure.
            two.Air.Volume += one.Air.Volume;
            _atmos.Merge(two.Air, one.Air.RemoveRatio(1f));

            if (user != null)
                _adminLog.Add(LogType.Explosion, LogImpact.High, $"{ToPrettyString(user.Value):user} opened {ToPrettyString(ent):valve} at {Transform(ent).Coordinates}");
            else
                _adminLog.Add(LogType.Explosion, LogImpact.High, $"{ToPrettyString(ent):valve} was opened at {Transform(ent).Coordinates}");
        }
        else
        {
            _atmos.Merge(one.Air, two.Air.RemoveRatio(one.Air.Volume / two.Air.Volume));
            two.Air.Volume = ent.Comp.TankTwoVolume;
        }

        ent.Comp.Open = open;
        return true;
    }

    private (GasTankComponent One, GasTankComponent Two)? GetTanks(Entity<TransferValveComponent> ent)
    {
        if (!TryComp<GasTankComponent>(_itemSlots.GetItemOrNull(ent, ent.Comp.TankOneSlot), out var one)
            || !TryComp<GasTankComponent>(_itemSlots.GetItemOrNull(ent, ent.Comp.TankTwoSlot), out var two))
            return null;

        return (one, two);
    }

    private void UpdateVisuals(Entity<TransferValveComponent> ent)
    {
        var one = _itemSlots.GetItemOrNull(ent, ent.Comp.TankOneSlot) != null;
        var two = _itemSlots.GetItemOrNull(ent, ent.Comp.TankTwoSlot) != null;
        var device = _itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlot) != null;
        _appearance.SetData(ent, TransferValveVisuals.TankOne, one);
        _appearance.SetData(ent, TransferValveVisuals.TankTwo, two);
        _appearance.SetData(ent, TransferValveVisuals.Device, device);
        _appearance.SetData(ent, TransferValveVisuals.Empty, !one && !two && !device);
    }
}
