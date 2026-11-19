using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Starlight.Atmos.TransferValve;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Content.Shared.Trigger;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Atmos.TransferValve;

/// <summary>
/// Opens and closes tank transfer valves.
/// </summary>
public sealed partial class TransferValveSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    [SubscribeLocalEvent]
    private void OnTrigger(Entity<TransferValveComponent> ent, ref TriggerEvent args)
    {
        // Stops something that fires every tick from flapping the valve.
        if (_timing.CurTime < ent.Comp.NextTrigger)
            return;

        ent.Comp.NextTrigger = _timing.CurTime + ent.Comp.TriggerCooldown;
        SetOpen(ent.AsNullable(), !ent.Comp.Open, args.User);
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<TransferValveComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetTanks(ent) is null)
            return;

        var user = args.User;
        var open = ent.Comp.Open;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(open ? "transfer-valve-verb-close" : "transfer-valve-verb-open"),
            Act = () => SetOpen(ent.AsNullable(), !open, user),
        });
    }

    /// <summary>
    /// Using the valve on a tank hangs the tank on it.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<TransferValveComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } tank)
            return;

        args.Handled = _itemSlots.TryInsert(ent, ent.Comp.TankOneSlot, tank, args.User)
            || _itemSlots.TryInsert(ent, ent.Comp.TankTwoSlot, tank, args.User);
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<TransferValveComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.Wired && TryComp<StackComponent>(args.Used, out var stack) && stack.StackTypeId == ent.Comp.CableStack)
        {
            args.Handled = true;
            if (!_stack.TryUse((args.Used, stack), ent.Comp.CableAmount))
            {
                _popup.PopupEntity(Loc.GetString("transfer-valve-need-cable", ("amount", ent.Comp.CableAmount)), ent, args.User);
                return;
            }

            SetWired(ent, true);
            _popup.PopupEntity(Loc.GetString("transfer-valve-wired"), ent, args.User);
            return;
        }

        if (ent.Comp.Wired && _tool.HasQuality(args.Used, SharedToolSystem.CutQuality))
        {
            args.Handled = true;
            SetWired(ent, false);
            _container.TryRemoveFromContainer(ent.Owner, true);
            _stack.SpawnAtPosition(ent.Comp.CableAmount, ent.Comp.CableStack, Transform(ent).Coordinates);
            _popup.PopupEntity(Loc.GetString("transfer-valve-unwired"), ent, args.User);
        }
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
            LogOpened((ent, ent.Comp), user);
        }
        else
        {
            _atmos.Merge(one.Air, two.Air.RemoveRatio(one.Air.Volume / two.Air.Volume));
            two.Air.Volume = ent.Comp.TankTwoVolume;
        }

        ent.Comp.Open = open;
        return true;
    }

    private void LogOpened(Entity<TransferValveComponent> ent, EntityUid? user)
    {
        var device = _itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlot);
        var deviceName = device == null ? "nothing" : ToPrettyString(device.Value).ToString();
        if (user != null)
            _adminLog.Add(LogType.Explosion, LogImpact.High, $"{ToPrettyString(user.Value):user} opened {ToPrettyString(ent):valve} with {deviceName} attached at {Transform(ent).Coordinates}");
        else
            _adminLog.Add(LogType.Explosion, LogImpact.High, $"{ToPrettyString(ent):valve} with {deviceName} attached was opened at {Transform(ent).Coordinates}");
    }

    private void SetWired(Entity<TransferValveComponent> ent, bool wired)
    {
        ent.Comp.Wired = wired;
        _clothing.SetSlots(ent, wired ? SlotFlags.BACK : SlotFlags.NONE);
        UpdateVisuals(ent);
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
        var one = TankState(ent, ent.Comp.TankOneSlot);
        var two = TankState(ent, ent.Comp.TankTwoSlot);
        _appearance.SetData(ent, TransferValveVisuals.TankOne, one);
        _appearance.SetData(ent, TransferValveVisuals.TankTwo, two);
        _appearance.SetData(ent, TransferValveVisuals.Device, _itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlot) != null);
        _appearance.SetData(ent, TransferValveVisuals.Wired, ent.Comp.Wired);

        // Back sprite: "l" for the second tank, "r" for the first.
        var prefix = (two != string.Empty ? "l" : "") + (one != string.Empty ? "r" : "");
        _clothing.SetEquippedPrefix(ent, prefix == string.Empty ? null : prefix);
    }

    private string TankState(Entity<TransferValveComponent> ent, string slot)
    {
        if (_itemSlots.GetItemOrNull(ent, slot) is not { } tank)
            return string.Empty;

        if (MetaData(tank).EntityPrototype is { } proto)
        {
            foreach (var parent in _proto.EnumerateParents<EntityPrototype>(proto, includeSelf: true))
            {
                if (ent.Comp.TankStates.TryGetValue(parent.ID, out var state))
                    return state;
            }
        }

        return ent.Comp.DefaultTankState;
    }
}
