using Content.Server._Starlight.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.Chemistry.Components;

/// <summary>
/// A machine that keeps puffing see-through smoke made from a copy of its tank's reagents.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SmokeMachineSystem))]
public sealed partial class SmokeMachineComponent : Component
{
    /// <summary>
    /// The solution the smoke is made from.
    /// </summary>
    [DataField]
    public string Solution = "tank";

    /// <summary>
    /// Whether it's switched on.
    /// </summary>
    [DataField]
    public bool Enabled;

    /// <summary>
    /// Puff size, from 1 to <see cref="MaxSetting"/>.
    /// </summary>
    [DataField]
    public int Setting = 1;

    /// <summary>
    /// The biggest puff size.
    /// </summary>
    [DataField]
    public int MaxSetting = 3;

    /// <summary>
    /// The tank loses one unit for every this many tiles a puff covers.
    /// </summary>
    [DataField]
    public float Efficiency = 20f;

    /// <summary>
    /// Most reagent a single puff carries. It's a copy; the tank only pays for the tiles.
    /// </summary>
    [DataField]
    public FixedPoint2 PuffVolume = 20;

    /// <summary>
    /// How long a puff's smoke lasts, in seconds.
    /// </summary>
    [DataField]
    public float SmokeDuration = 20f;

    /// <summary>
    /// Time between puffs while it runs.
    /// </summary>
    [DataField]
    public TimeSpan PuffInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// When it next tries to puff.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPuff;

    /// <summary>
    /// The smoke a puff is made of.
    /// </summary>
    [DataField]
    public EntProtoId SmokePrototype = "SmokeMachineHaze";

    /// <summary>
    /// How many different reagents plumbing may fill the tank with.
    /// </summary>
    [DataField]
    public int MaxReagentKinds = 5;

    /// <summary>
    /// Power draw while switched off.
    /// </summary>
    [DataField]
    public float IdleLoad = 100f;

    /// <summary>
    /// Power draw while running at the top setting; lower settings draw their share of it.
    /// </summary>
    [DataField]
    public float ActiveLoad = 1000f;
}
