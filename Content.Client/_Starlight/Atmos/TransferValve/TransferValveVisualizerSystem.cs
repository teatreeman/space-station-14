using Content.Shared._Starlight.Atmos.TransferValve;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Atmos.TransferValve;

/// <summary>
/// Draws each attached tank as its own kind of tank, plus the trigger and cable straps.
/// </summary>
public sealed class TransferValveVisualizerSystem : VisualizerSystem<TransferValveComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, TransferValveComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        AppearanceSystem.TryGetData(uid, TransferValveVisuals.TankOne, out string? one, args.Component);
        AppearanceSystem.TryGetData(uid, TransferValveVisuals.TankTwo, out string? two, args.Component);
        AppearanceSystem.TryGetData(uid, TransferValveVisuals.Device, out bool device, args.Component);
        AppearanceSystem.TryGetData(uid, TransferValveVisuals.Wired, out bool wired, args.Component);

        SetTank(sprite, TransferValveVisualLayers.TankOne, one);
        SetTank(sprite, TransferValveVisualLayers.TankTwo, two);
        SpriteSystem.LayerSetVisible(sprite, TransferValveVisualLayers.Device, device);
        SpriteSystem.LayerSetVisible(sprite, TransferValveVisualLayers.Cables, wired);

        var empty = string.IsNullOrEmpty(one) && string.IsNullOrEmpty(two) && !device;
        SpriteSystem.LayerSetRsiState(sprite, TransferValveVisualLayers.Base, empty ? "icon" : "valve");
    }

    private void SetTank(Entity<SpriteComponent?> sprite, TransferValveVisualLayers layer, string? state)
    {
        var visible = !string.IsNullOrEmpty(state);
        SpriteSystem.LayerSetVisible(sprite, layer, visible);
        if (visible)
            SpriteSystem.LayerSetRsiState(sprite, layer, state!);
    }
}
