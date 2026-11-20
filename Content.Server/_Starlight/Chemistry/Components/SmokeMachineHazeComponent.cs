using Content.Server._Starlight.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Chemistry.Components;

/// <summary>
/// One tile of a smoke machine's haze. People in it breathe and touch the whole carried copy as with any smoke, but the
/// floor only gets this tile's share of what actually left the machine's tank, so a puff never leaves more puddle than
/// it cost.
/// </summary>
[RegisterComponent]
[Access(typeof(SmokeMachineSystem))]
public sealed partial class SmokeMachineHazeComponent : Component
{
    /// <summary>
    /// The puff this tile belongs to. Not saved: haze loaded from a save leaves the floor alone.
    /// </summary>
    [ViewVariables]
    public int PuffId;

    /// <summary>
    /// Most units of the carried mix this tile puts on its floor.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 FloorShare;

    /// <summary>
    /// Whether this tile has reacted with its floor yet.
    /// </summary>
    [ViewVariables]
    public bool FloorDone;

    /// <summary>
    /// What this tile actually put on its floor.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 FloorReacted;
}
