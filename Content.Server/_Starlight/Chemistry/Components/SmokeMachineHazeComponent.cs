namespace Content.Server._Starlight.Chemistry.Components;

/// <summary>
/// Marks a smoke machine's haze. It doses people through the air like other chemical smoke, but never reacts with the
/// floor, and people with a gas-tight mask or working internals are left alone entirely.
/// </summary>
[RegisterComponent]
public sealed partial class SmokeMachineHazeComponent : Component;
