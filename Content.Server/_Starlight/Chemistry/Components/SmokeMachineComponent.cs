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
    /// The spread amount the smoke gets at each setting. On open floor the station's smoke comes out at about 25, 85
    /// and 181 tiles with these.
    /// </summary>
    [DataField]
    public List<int> PuffSpread = new() { 14, 40, 180 };

    /// <summary>
    /// The biggest puff size.
    /// </summary>
    public int MaxSetting => PuffSpread.Count;

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
    /// Units of the mix that condense and drip onto the floor beside it with each puff, paid from the tank on top of
    /// the puff. Two puffs of space lube make a slippery puddle, even with some of the first evaporating in between.
    /// </summary>
    [DataField]
    public FixedPoint2 DripVolume = 10;

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
    /// The smoke a puff is made of. It should have <see cref="SmokeMachineHazeComponent"/>, which keeps it off floors and out of masks.
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
