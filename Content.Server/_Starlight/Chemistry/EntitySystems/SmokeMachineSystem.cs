using System.Linq;
using Content.Server._Starlight.Chemistry.Components;
using Content.Server._Starlight.Plumbing;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Spreader;
using Content.Shared._Starlight.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Smoking;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Chemistry.EntitySystems;

/// <summary>
/// Runs the smoke machine: every couple of seconds, while switched on, powered and anchored, it lets out a puff of
/// smoke carrying a copy of its tank's mix and loses a little of the tank for it.
/// </summary>
public sealed partial class SmokeMachineSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
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
    /// What each live puff may still put on floors, and how many of its haze tiles exist. Smoke can spread past its
    /// tile count, so the per-tile share alone doesn't cap a puff; this does.
    /// </summary>
    private readonly Dictionary<int, PuffBudget> _puffs = new();

    private int _nextPuff = 1;

    private sealed class PuffBudget(FixedPoint2 remaining)
    {
        public FixedPoint2 Remaining = remaining;
        public int Tiles;
    }

    /// <summary>
    /// How many tiles a puff at this setting covers: a diamond of radius three tiles per setting step.
    /// </summary>
    public static int PuffTiles(int setting)
    {
        var radius = 3 * setting;
        return 1 + 2 * radius * (radius + 1);
    }

    /// <summary>
    /// How much the tank loses for one puff at the machine's current setting.
    /// </summary>
    public FixedPoint2 TankLoss(Entity<SmokeMachineComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return FixedPoint2.Zero;

        return FixedPoint2.New(PuffTiles(ent.Comp.Setting) / ent.Comp.Efficiency);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ReactHazeFloors();

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

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<SmokeComponent>(anchored))
                return false;
        }

        var tiles = PuffTiles(ent.Comp.Setting);
        var carried = tank.Clone();
        if (carried.Volume > ent.Comp.PuffVolume)
            carried = carried.SplitSolution(ent.Comp.PuffVolume);

        var lost = _solutions.SplitSolution(soln.Value, TankLoss(ent)).Volume;

        var smoke = Spawn(ent.Comp.SmokePrototype, _map.GridTileToLocal(gridUid, grid, tile));
        var haze = EnsureComp<SmokeMachineHazeComponent>(smoke);
        var puff = _nextPuff++;
        _puffs[puff] = new PuffBudget(lost) { Tiles = 1 };
        haze.PuffId = puff;
        haze.FloorShare = FixedPoint2.FromCents(lost.Value / tiles);
        StartHaze(smoke, carried, ent.Comp.SmokeDuration, tiles - 1);

        if (tank.Volume <= 0)
            SetEnabled((ent, ent.Comp), false);

        return true;
    }

    /// <summary>
    /// Starts a haze tile as ordinary smoke, but empty at first so the smoke system's one-off floor reaction has
    /// nothing to put down; this system wets the floor itself, within the puff's budget.
    /// </summary>
    private void StartHaze(EntityUid haze, Solution carried, float duration, int spreadAmount)
    {
        _smoke.StartSmoke(haze, new Solution(), duration, spreadAmount);
        if (!TryComp<SmokeComponent>(haze, out var smoke) ||
            !_solutions.TryGetSolution(haze, SmokeComponent.SolutionName, out var soln, out _))
        {
            return;
        }

        smoke.TransferRate = carried.Volume / duration;
        _solutions.TryAddSolution(soln.Value, carried);
        _appearance.SetData(haze, SmokeVisuals.Color, carried.GetColor(_proto));
    }

    /// <summary>
    /// Spreads haze the same way the smoke system spreads smoke, starting each new tile through
    /// <see cref="StartHaze"/>, then leaves the smoke system no free tiles to spread to itself.
    /// </summary>
    [SubscribeLocalEvent(before: [typeof(SmokeSystem)])]
    private void OnHazeSpread(Entity<SmokeMachineHazeComponent> ent, ref SpreadNeighborsEvent args)
    {
        if (!TryComp<SmokeComponent>(ent, out var smoke) ||
            smoke.SpreadAmount == 0 ||
            args.NeighborFreeTiles.Count == 0 ||
            Prototype(ent) is not { } prototype ||
            !_solutions.TryGetSolution(ent.Owner, SmokeComponent.SolutionName, out _, out var carried))
        {
            return;
        }

        var duration = CompOrNull<TimedDespawnComponent>(ent)?.Lifetime ?? smoke.Duration;
        var perSpread = Math.Max(0, smoke.SpreadAmount / args.NeighborFreeTiles.Count);
        foreach (var neighbor in args.NeighborFreeTiles)
        {
            var coords = _map.GridTileToLocal(neighbor.Tile.GridUid, neighbor.Grid, neighbor.Tile.GridIndices);
            var child = Spawn(prototype.ID, coords);
            JoinPuff((child, EnsureComp<SmokeMachineHazeComponent>(child)), ent);
            smoke.SpreadAmount -= args.NeighborFreeTiles.Count;
            StartHaze(child, carried.Clone(), duration, perSpread);

            if (smoke.SpreadAmount == 0)
                break;
        }

        args.Updates--;
        args.NeighborFreeTiles.Clear();
    }

    /// <summary>
    /// Makes a haze tile part of another tile's puff: same floor share, same floor budget.
    /// </summary>
    public void JoinPuff(Entity<SmokeMachineHazeComponent> tile, Entity<SmokeMachineHazeComponent> from)
    {
        if (_puffs.TryGetValue(tile.Comp.PuffId, out var old))
            old.Tiles--;

        tile.Comp.PuffId = from.Comp.PuffId;
        tile.Comp.FloorShare = from.Comp.FloorShare;
        if (_puffs.TryGetValue(tile.Comp.PuffId, out var budget))
            budget.Tiles++;
    }

    [SubscribeLocalEvent]
    private void OnHazeShutdown(Entity<SmokeMachineHazeComponent> ent, ref ComponentShutdown args)
    {
        if (!_puffs.TryGetValue(ent.Comp.PuffId, out var budget))
            return;

        budget.Tiles--;
        if (budget.Tiles <= 0)
            _puffs.Remove(ent.Comp.PuffId);
    }

    /// <summary>
    /// Wets each new haze tile's floor with its share of the carried mix.
    /// </summary>
    private void ReactHazeFloors()
    {
        var query = EntityQueryEnumerator<SmokeMachineHazeComponent, SmokeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var haze, out var smoke, out var xform))
        {
            if (haze.FloorDone)
                continue;

            haze.FloorDone = true;
            if (!_puffs.TryGetValue(haze.PuffId, out var budget) ||
                xform.GridUid is not { } gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var grid) ||
                !_solutions.ResolveSolution(uid, SmokeComponent.SolutionName, ref smoke.Solution, out var carried))
            {
                continue;
            }

            var share = FixedPoint2.Min(FixedPoint2.Min(haze.FloorShare, budget.Remaining), carried.Volume);
            if (share <= 0)
                continue;

            budget.Remaining -= share;
            haze.FloorReacted = share;
            var floor = carried.Clone().SplitSolution(share);
            var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
            foreach (var (reagent, quantity) in floor.Contents.ToArray())
            {
                if (quantity > 0 && _proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var reagentProto))
                    reagentProto.ReactionTile(tile, quantity, EntityManager, reagent.Data);
            }
        }
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
        if (!ent.Comp.Enabled || args.Cancelled || !args.Open)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("smoke-machine-switch-off-first"), ent, user);
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
