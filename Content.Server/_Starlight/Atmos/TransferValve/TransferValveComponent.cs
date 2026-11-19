using Robust.Shared.Audio;

namespace Content.Server._Starlight.Atmos.TransferValve;

/// <summary>
/// A valve between two gas tanks. Opening it pours the first tank into the second.
/// </summary>
[RegisterComponent]
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
    /// Played when the valve opens or closes.
    /// </summary>
    [DataField]
    public SoundSpecifier ValveSound = new SoundPathSpecifier("/Audio/Items/hiss.ogg");
}
