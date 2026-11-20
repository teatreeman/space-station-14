using Content.Server._Starlight.Chemistry.Events;

// ReSharper disable CheckNamespace
namespace Content.Server.Fluids.EntitySystems;

public sealed partial class SmokeSystem
{
    /// <summary>
    /// Lets a smoke decide what someone's gear keeps out. Returns true if it doesn't reach them at all; otherwise sets
    /// <paramref name="blockInhale"/> if they don't breathe it in.
    /// </summary>
    private bool Shields(EntityUid target, EntityUid smoke, ref bool blockInhale)
    {
        var ev = new SmokeShieldEvent(target);
        RaiseLocalEvent(smoke, ref ev);
        blockInhale |= ev.BlockInhale;
        return ev.BlockAll;
    }
}
