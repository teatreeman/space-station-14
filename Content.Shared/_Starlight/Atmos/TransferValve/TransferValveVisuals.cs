using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Atmos.TransferValve;

[Serializable, NetSerializable]
public enum TransferValveVisuals : byte
{
    /// <summary>
    /// Sprite state of the first tank, or empty when there isn't one.
    /// </summary>
    TankOne,
    TankTwo,
    Device,
    Wired,
}

public enum TransferValveVisualLayers : byte
{
    Base,
    TankOne,
    TankTwo,
    Device,
    Cables,
}
