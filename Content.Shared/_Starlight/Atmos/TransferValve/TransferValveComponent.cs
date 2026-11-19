using Content.Shared.Stacks;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Atmos.TransferValve;

/// <summary>
/// A valve between two gas tanks. Opening it pours the first tank into the second.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class TransferValveComponent : Component
{
    /// <summary>
    /// Item slot holding the tank that gets emptied.
    /// </summary>
    [DataField]
    public string TankOneSlot = "tank_one";

    /// <summary>
    /// Item slot holding the tank that gets filled.
    /// </summary>
    [DataField]
    public string TankTwoSlot = "tank_two";

    /// <summary>
    /// Item slot for the trigger. PayloadCase only passes triggers on from a container with this name.
    /// </summary>
    [DataField]
    public string TriggerSlot = "payloadTrigger";

    /// <summary>
    /// Whether the valve is open.
    /// </summary>
    [DataField]
    public bool Open;

    /// <summary>
    /// The second tank's own volume, put back when the valve closes.
    /// </summary>
    [DataField]
    public float TankTwoVolume;

    /// <summary>
    /// Trigger keys that flip the valve. A timer starting has its own key, so it doesn't count.
    /// </summary>
    [DataField]
    public HashSet<string> KeysIn = new() { TriggerSystem.DefaultTriggerKey };

    /// <summary>
    /// A trigger flips the valve, but not again until this long has passed.
    /// </summary>
    [DataField]
    public TimeSpan TriggerCooldown = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// When a trigger can flip the valve again.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextTrigger;

    /// <summary>
    /// Sprite state drawn for each kind of tank, matched on the tank's prototype or its parents.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, string> TankStates = new();

    /// <summary>
    /// Sprite state for tanks missing from <see cref="TankStates"/>.
    /// </summary>
    [DataField]
    public string DefaultTankState = "generic";

    /// <summary>
    /// Whether cable straps have been added, letting it be worn on the back.
    /// </summary>
    [DataField]
    public bool Wired;

    /// <summary>
    /// The cable used for the straps.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype> CableStack = "Cable";

    /// <summary>
    /// How much cable the straps take.
    /// </summary>
    [DataField]
    public int CableAmount = 15;

    /// <summary>
    /// Played when the valve opens or closes.
    /// </summary>
    [DataField]
    public SoundSpecifier ValveSound = new SoundCollectionSpecifier("TransferValveTurn");
}
