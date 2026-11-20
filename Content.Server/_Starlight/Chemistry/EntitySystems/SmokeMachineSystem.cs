using System.Linq;
using Content.Server._Starlight.Chemistry.Components;
using Content.Server._Starlight.Plumbing;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Spreader;
using Content.Shared._Starlight.Chemistry;
using Content.Shared._Starlight.Construction;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Slippery;
using Content.Shared.Smoking;
using Content.Shared.Spreader;
using Content.Shared.SubFloor;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Chemistry.EntitySystems;

/// <summary>
/// Runs the smoke machine: every couple of seconds, while switched on, powered and anchored, it lets out a puff of
/// haze carrying a copy of its tank's mix and loses a little of the tank for it. The machine spreads each puff itself,
/// a ring of tiles at a time, so a puff reaches its full size in about a second whatever other smoke is doing.
/// </summary>
public sealed partial class SmokeMachineSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SpreaderSystem _spreader = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<EdgeSpreaderPrototype> SmokeSpreader = "Smoke";

    /// <summary>
    /// Time between rings of a spreading puff.
    /// </summary>
    private static readonly TimeSpan SpreadDelay = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// How often haze touches the things lying in it.
    /// </summary>
    private static readonly TimeSpan ExposeDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Share of a tile's mix each of those touches uses: two seconds of a twenty second haze.
    /// </summary>
    private const float ExposeShare = 0.1f;

    private readonly Dictionary<int, Puff> _puffs = new();
    private readonly HashSet<EntityUid> _onTile = new();
    private int _nextPuff = 1;

    /// <summary>
    /// One puff: its haze tiles, how far it still has to spread, and what its floors may still be wetted with.
    /// </summary>
    private sealed class Puff(int id, EntProtoId haze, Solution carried, int target, float duration, FixedPoint2 floorBudget, TimeSpan end)
    {
        public readonly int Id = id;
        public readonly EntProtoId Haze = haze;
        public readonly Solution Carried = carried;
        public readonly int Target = target;
        public readonly float Duration = duration;
        public readonly TimeSpan End = end;
        public readonly FixedPoint2 FloorShare = FixedPoint2.FromCents(floorBudget.Value / target);
        public FixedPoint2 FloorBudget = floorBudget;
        public readonly HashSet<EntityUid> Tiles = new();
        public List<EntityUid> Frontier = new();
        public int Spawned;
        public TimeSpan NextSpread;
        public TimeSpan NextExpose;
    }

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

        ReactHazeFloors();

        var now = _timing.CurTime;
        foreach (var puff in _puffs.Values.ToArray())
        {
            if (puff.Frontier.Count > 0 && now >= puff.NextSpread)
                SpreadRing(puff, now);

            if (now >= puff.NextExpose)
                ExposeItems(puff);
        }

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

        var lost = _solutions.SplitSolution(soln.Value, TankLoss(ent)).Volume;

        var now = _timing.CurTime;
        var id = _nextPuff++;
        var puff = new Puff(id, ent.Comp.SmokePrototype, carried, SmokeMachinePuffs.Tiles(ent.Comp.Setting), ent.Comp.SmokeDuration, lost,
            now + TimeSpan.FromSeconds(ent.Comp.SmokeDuration))
        {
            NextSpread = now + SpreadDelay,
            NextExpose = now + ExposeDelay,
        };
        _puffs[id] = puff;
        puff.Frontier.Add(SpawnHaze(puff, _map.GridTileToLocal(gridUid, grid, tile), now));

        if (tank.Volume <= 0)
            SetEnabled((ent, ent.Comp), false);

        return true;
    }

    /// <summary>
    /// Spawns one tile of a puff's haze. People standing in it get ordinary smoke, breathing and touching its mix over
    /// the puff's whole lifetime. It starts empty so the smoke system's one-off floor reaction has nothing to put
    /// down (this system wets the floor within the puff's budget), and it never spreads on its own.
    /// </summary>
    private EntityUid SpawnHaze(Puff puff, EntityCoordinates coords, TimeSpan now)
    {
        var uid = Spawn(puff.Haze, coords);
        var haze = EnsureComp<SmokeMachineHazeComponent>(uid);
        haze.PuffId = puff.Id;
        puff.Tiles.Add(uid);
        puff.Spawned++;

        // The whole puff clears together, like the tiles it spread from.
        _smoke.StartSmoke(uid, new Solution(), (float) (puff.End - now).TotalSeconds, 0);
        RemComp<ActiveEdgeSpreaderComponent>(uid);

        if (TryComp<SmokeComponent>(uid, out var smoke) &&
            _solutions.TryGetSolution(uid, SmokeComponent.SolutionName, out var soln, out _))
        {
            smoke.TransferRate = puff.Carried.Volume / puff.Duration;
            _solutions.TryAddSolution(soln.Value, puff.Carried.Clone());
            _appearance.SetData(uid, SmokeVisuals.Color, puff.Carried.GetColor(_proto));
        }

        return uid;
    }

    /// <summary>
    /// Spreads a puff one ring further, onto open tiles next to its outer edge that don't have smoke yet, until it
    /// covers its size.
    /// </summary>
    private void SpreadRing(Puff puff, TimeSpan now)
    {
        puff.NextSpread = now + SpreadDelay;
        var ring = new List<EntityUid>();
        var claimed = new HashSet<(EntityUid, Vector2i)>();
        foreach (var edge in puff.Frontier)
        {
            if (puff.Spawned >= puff.Target)
                break;

            if (TerminatingOrDeleted(edge))
                continue;

            _spreader.GetNeighbors(edge, Transform(edge), SmokeSpreader, out var free, out _, out _);
            foreach (var (grid, tile) in free)
            {
                if (puff.Spawned >= puff.Target)
                    break;

                if (claimed.Add((tile.GridUid, tile.GridIndices)))
                    ring.Add(SpawnHaze(puff, _map.GridTileToLocal(tile.GridUid, grid, tile.GridIndices), now));
            }
        }

        puff.Frontier = puff.Spawned >= puff.Target ? new List<EntityUid>() : ring;
    }

    /// <summary>
    /// Every couple of seconds each haze tile touches the loose things in it (not people, not what's under the floor)
    /// with a share of its mix.
    /// </summary>
    private void ExposeItems(Puff puff)
    {
        puff.NextExpose += ExposeDelay;
        var dose = puff.Carried.Clone().SplitSolution(puff.Carried.Volume * ExposeShare);
        foreach (var haze in puff.Tiles.ToArray())
        {
            var xform = Transform(haze);
            if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            _onTile.Clear();
            _lookup.GetLocalEntitiesIntersecting(gridUid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates), _onTile,
                flags: LookupFlags.Uncontained, gridComp: grid);
            foreach (var thing in _onTile)
            {
                if (!HasComp<ReactiveComponent>(thing) ||
                    HasComp<MobStateComponent>(thing) ||
                    HasComp<SmokeComponent>(thing) ||
                    TryComp<SubFloorHideComponent>(thing, out var subfloor) && subfloor.IsUnderCover)
                {
                    continue;
                }

                foreach (var reagent in dose.Contents)
                {
                    _reactive.ReactionEntity(thing, ReactionMethod.Touch, reagent);
                }
            }
        }
    }

    /// <summary>
    /// Makes a haze tile part of another tile's puff, sharing its floor budget.
    /// </summary>
    public void JoinPuff(Entity<SmokeMachineHazeComponent> tile, Entity<SmokeMachineHazeComponent> from)
    {
        if (_puffs.TryGetValue(tile.Comp.PuffId, out var old))
            old.Tiles.Remove(tile);

        tile.Comp.PuffId = from.Comp.PuffId;
        if (_puffs.TryGetValue(tile.Comp.PuffId, out var puff))
            puff.Tiles.Add(tile);
    }

    [SubscribeLocalEvent]
    private void OnHazeShutdown(Entity<SmokeMachineHazeComponent> ent, ref ComponentShutdown args)
    {
        if (!_puffs.TryGetValue(ent.Comp.PuffId, out var puff))
            return;

        puff.Tiles.Remove(ent);
        puff.Frontier.Remove(ent);
        if (puff.Tiles.Count == 0)
            _puffs.Remove(ent.Comp.PuffId);
    }

    /// <summary>
    /// Wets each new haze tile's floor with its share of the puff's mix, while the puff's floor budget lasts.
    /// </summary>
    private void ReactHazeFloors()
    {
        var query = EntityQueryEnumerator<SmokeMachineHazeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var haze, out var xform))
        {
            if (haze.FloorDone)
                continue;

            haze.FloorDone = true;
            if (!_puffs.TryGetValue(haze.PuffId, out var puff) ||
                xform.GridUid is not { } gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var grid))
            {
                continue;
            }

            var share = FixedPoint2.Min(FixedPoint2.Min(puff.FloorShare, puff.FloorBudget), puff.Carried.Volume);
            if (share <= 0)
                continue;

            puff.FloorBudget -= share;
            haze.FloorReacted = share;
            var floor = puff.Carried.Clone().SplitSolution(share);
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
