using Content.Server._Starlight.Chemistry.Components;
using Content.Server._Starlight.Plumbing;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Chemistry;
using Content.Shared._Starlight.Construction;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Slippery;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Chemistry.EntitySystems;

/// <summary>
/// Runs the smoke machine: every couple of seconds, while switched on, powered and anchored, it lets out a puff of
/// haze carrying a copy of its tank's mix and loses a little of the tank for it.
/// </summary>
public sealed partial class SmokeMachineSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>
    /// How much the tank loses for one puff at the machine's current setting.
    /// </summary>
    public FixedPoint2 TankLoss(Entity<SmokeMachineComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return FixedPoint2.Zero;

        return FixedPoint2.New(SmokeMachinePuffs.Tiles(ent.Comp.Setting) / ent.Comp.Efficiency);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SmokeMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled || now < comp.NextPuff)
                continue;

            comp.NextPuff = now + comp.PuffInterval;
            TryPuff((uid, comp));
        }
    }

    /// <summary>
    /// One machine step: switches off if it can't run, otherwise puffs unless smoke still sits on its tile.
    /// Returns whether a puff came out.
    /// </summary>
    public bool TryPuff(Entity<SmokeMachineComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        var xform = Transform(ent);
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var tank) ||
            tank.Volume <= 0 ||
            !_power.IsPowered(ent.Owner) ||
            !xform.Anchored)
        {
            SetEnabled((ent, ent.Comp), false);
            return false;
        }

        if (_transform.GetGrid((ent.Owner, xform)) is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        // Foam is smoke you slip on; only real smoke holds the next puff back.
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<SmokeComponent>(anchored) && !HasComp<SlipperyComponent>(anchored))
                return false;
        }

        var carried = tank.Clone();
        if (carried.Volume > ent.Comp.PuffVolume)
            carried = carried.SplitSolution(ent.Comp.PuffVolume);

        _solutions.SplitSolution(soln.Value, TankLoss(ent));

        // The rest is ordinary chemical smoke, spreading and dosing people the same way as a smoke grenade.
        var smoke = Spawn(ent.Comp.SmokePrototype, _map.GridTileToLocal(gridUid, grid, tile));
        _smoke.StartSmoke(smoke, carried, ent.Comp.SmokeDuration, ent.Comp.PuffSpread[ent.Comp.Setting - 1]);

        if (tank.Volume <= 0)
            SetEnabled((ent, ent.Comp), false);

        return true;
    }

    /// <summary>
    /// Switches the machine on or off. Someone starting it with reagents inside is logged and admins are told.
    /// </summary>
    public void SetEnabled(Entity<SmokeMachineComponent?> ent, bool enabled, EntityUid? user = null)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        if (enabled)
        {
            ent.Comp.NextPuff = _timing.CurTime;
            if (user != null && _solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var tank) && tank.Volume > 0)
            {
                var contents = SharedSolutionContainerSystem.ToPrettyString(tank);
                _adminLog.Add(LogType.Action, LogImpact.Medium,
                    $"{ToPrettyString(user.Value):user} switched on {ToPrettyString(ent.Owner):machine} holding {contents}");
                _chat.SendAdminAlert(Loc.GetString("smoke-machine-admin-alert",
                    ("user", ToPrettyString(user.Value)),
                    ("machine", ToPrettyString(ent.Owner)),
                    ("contents", contents)));
            }
        }

        UpdateLoad((ent, ent.Comp));
        _appearance.SetData(ent, SmokeMachineVisuals.Running, enabled);
        UpdateUi((ent, ent.Comp));
    }

    /// <summary>
    /// Picks the puff size, clamped to the machine's range.
    /// </summary>
    public void SetSetting(Entity<SmokeMachineComponent?> ent, int setting)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Setting = Math.Clamp(setting, 1, ent.Comp.MaxSetting);
        UpdateLoad((ent, ent.Comp));
        UpdateUi((ent, ent.Comp));
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<SmokeMachineComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Setting = Math.Clamp(ent.Comp.Setting, 1, ent.Comp.MaxSetting);
        UpdateLoad(ent);
        _appearance.SetData(ent, SmokeMachineVisuals.Running, ent.Comp.Enabled);
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<SmokeMachineComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("smoke-machine-examine-plumbing", ("kinds", ent.Comp.MaxReagentKinds)));
    }

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<SmokeMachineComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnSolutionChanged(Entity<SmokeMachineComponent> ent, ref SolutionChangedEvent args)
    {
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnPullInto(Entity<SmokeMachineComponent> ent, ref PlumbingPullIntoAttemptEvent args)
    {
        if (args.Cancelled || !_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var tank))
            return;

        // Plumbing can top up what's already in, but only bring in so many different reagents.
        var kinds = 0;
        foreach (var (reagent, quantity) in tank.Contents)
        {
            if (quantity <= 0)
                continue;

            if (reagent.Prototype == args.ReagentPrototype)
                return;

            kinds++;
        }

        if (kinds >= ent.Comp.MaxReagentKinds)
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnToggle(Entity<SmokeMachineComponent> ent, ref SmokeMachineToggleMessage args)
    {
        if (ent.Comp.Enabled)
        {
            SetEnabled(ent.AsNullable(), false, args.Actor);
            return;
        }

        string? refusal = null;
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var tank) || tank.Volume <= 0)
            refusal = "smoke-machine-empty";
        else if (!Transform(ent).Anchored)
            refusal = "smoke-machine-unanchored";
        else if (!_power.IsPowered(ent.Owner))
            refusal = "smoke-machine-unpowered";

        if (refusal != null)
        {
            _popup.PopupEntity(Loc.GetString(refusal), ent, args.Actor);
            return;
        }

        SetEnabled(ent.AsNullable(), true, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnSetSetting(Entity<SmokeMachineComponent> ent, ref SmokeMachineSetSettingMessage args)
    {
        SetSetting(ent.AsNullable(), args.Setting);
    }

    [SubscribeLocalEvent]
    private void OnPurge(Entity<SmokeMachineComponent> ent, ref SmokeMachinePurgeMessage args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var tank) || tank.Volume <= 0)
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):user} purged {ToPrettyString(ent.Owner):machine} of {SharedSolutionContainerSystem.ToPrettyString(tank)}");
        _solutions.RemoveAllSolution(soln.Value);
        SetEnabled(ent.AsNullable(), false);
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<SmokeMachineComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            SetEnabled(ent.AsNullable(), false);
    }

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<SmokeMachineComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            SetEnabled(ent.AsNullable(), false);
    }

    [SubscribeLocalEvent]
    private void OnAnchorAttempt(Entity<SmokeMachineComponent> ent, ref AnchorAttemptEvent args)
    {
        RefuseWhileRunning(ent, args, args.User);
    }

    [SubscribeLocalEvent]
    private void OnUnanchorAttempt(Entity<SmokeMachineComponent> ent, ref UnanchorAttemptEvent args)
    {
        RefuseWhileRunning(ent, args, args.User);
    }

    private void RefuseWhileRunning(Entity<SmokeMachineComponent> ent, CancellableEntityEventArgs args, EntityUid user)
    {
        if (!ent.Comp.Enabled || args.Cancelled)
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("smoke-machine-switch-off-first"), ent, user);
    }

    [SubscribeLocalEvent]
    private void OnPanelAttempt(Entity<SmokeMachineComponent> ent, ref AttemptChangePanelEvent args)
    {
        if (!ent.Comp.Enabled || args.Cancelled)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("smoke-machine-switch-off-first"), ent, user);
    }

    [SubscribeLocalEvent]
    private void OnConstructionAttempt(Entity<SmokeMachineComponent> ent, ref ConstructionInteractAttemptEvent args)
    {
        if (!ent.Comp.Enabled || args.Canceled)
            return;

        args.Canceled = true;
        _popup.PopupEntity(Loc.GetString("smoke-machine-switch-off-first"), ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnDeconstructed(Entity<SmokeMachineComponent> ent, ref MachineDeconstructedEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var tank) || tank.Volume <= 0)
            return;

        _puddle.TrySpillAt(Transform(ent).Coordinates, _solutions.SplitSolution(soln.Value, tank.Volume), out _);
    }

    private void UpdateLoad(Entity<SmokeMachineComponent> ent)
    {
        var load = ent.Comp.Enabled
            ? ent.Comp.ActiveLoad * ent.Comp.Setting / ent.Comp.MaxSetting
            : ent.Comp.IdleLoad;
        _power.SetLoad(ent.Owner, load);
    }

    private void UpdateUi(Entity<SmokeMachineComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, SmokeMachineUiKey.Key))
            return;

        var contents = new List<SmokeMachineTankEntry>();
        FixedPoint2 volume = 0;
        FixedPoint2 maxVolume = 0;
        if (_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var tank))
        {
            volume = tank.Volume;
            maxVolume = tank.MaxVolume;
            foreach (var (reagent, quantity) in tank.Contents)
            {
                contents.Add(new SmokeMachineTankEntry(reagent.Prototype, quantity));
            }
        }

        _ui.SetUiState(ent.Owner, SmokeMachineUiKey.Key, new SmokeMachineBoundUserInterfaceState(
            ent.Comp.Enabled, ent.Comp.Setting, ent.Comp.MaxSetting, volume, maxVolume, contents));
    }
}
