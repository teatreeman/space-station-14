using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Chemistry.Components;

/// <summary>
/// Marks a smoke machine's haze. People in it breathe and touch the whole carried mix like any smoke, but the floor
/// under each tile only gets <see cref="FloorShare"/> of it, so a puff leaves no more puddle than its tank lost.
/// </summary>
[RegisterComponent]
public sealed partial class SmokeMachineHazeComponent : Component
{
    /// <summary>
    /// Units of the carried mix each tile puts on its floor. A puff costs the tank 1u per 20 tiles; this is a bit under
    /// that, so a puff that spreads a little past its size still wets no more than the tank lost.
    /// </summary>
    [DataField]
    public FixedPoint2 FloorShare = 0.04;
}
