using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Atmos.TransferValve;

[Serializable, NetSerializable]
public enum TransferValveVisuals : byte
{
    TankOne,
    TankTwo,
    Device,
    Empty,
}
