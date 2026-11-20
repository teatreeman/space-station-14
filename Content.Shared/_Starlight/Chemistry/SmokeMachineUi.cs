using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Chemistry;

[Serializable, NetSerializable]
public enum SmokeMachineUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum SmokeMachineVisuals : byte
{
    Running,
}

/// <summary>
/// One reagent in the smoke machine's tank, for its window.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct SmokeMachineTankEntry(ProtoId<ReagentPrototype> Reagent, FixedPoint2 Quantity);

[Serializable, NetSerializable]
public sealed class SmokeMachineBoundUserInterfaceState(
    bool enabled,
    int setting,
    int maxSetting,
    FixedPoint2 volume,
    FixedPoint2 maxVolume,
    List<SmokeMachineTankEntry> contents) : BoundUserInterfaceState
{
    public readonly bool Enabled = enabled;
    public readonly int Setting = setting;
    public readonly int MaxSetting = maxSetting;
    public readonly FixedPoint2 Volume = volume;
    public readonly FixedPoint2 MaxVolume = maxVolume;
    public readonly List<SmokeMachineTankEntry> Contents = contents;
}

/// <summary>
/// Switches the smoke machine on or off.
/// </summary>
[Serializable, NetSerializable]
public sealed class SmokeMachineToggleMessage : BoundUserInterfaceMessage;

/// <summary>
/// Picks the puff size. The server clamps it to the machine's range.
/// </summary>
[Serializable, NetSerializable]
public sealed class SmokeMachineSetSettingMessage(int setting) : BoundUserInterfaceMessage
{
    public readonly int Setting = setting;
}

/// <summary>
/// Empties the smoke machine's tank.
/// </summary>
[Serializable, NetSerializable]
public sealed class SmokeMachinePurgeMessage : BoundUserInterfaceMessage;
