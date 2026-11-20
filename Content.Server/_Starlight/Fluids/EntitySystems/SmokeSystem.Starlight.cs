using Content.Server._Starlight.Chemistry.Events;

// ReSharper disable CheckNamespace
namespace Content.Server.Fluids.EntitySystems;

public sealed partial class SmokeSystem
{
    /// <summary>
    /// Whether this smoke leaves someone standing in it alone, as some smoke does for people with protection.
    /// </summary>
    private bool SkipsTarget(EntityUid target, EntityUid smoke)
    {
        var ev = new SmokeReactAttemptEvent(target);
        RaiseLocalEvent(smoke, ref ev);
        return ev.Cancelled;
    }
}
