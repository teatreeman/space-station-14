namespace Content.Server._Starlight.Chemistry.Events;

/// <summary>
/// Raised on a smoke tile before it reaches someone standing in it, so the smoke can decide what their gear keeps out.
/// </summary>
[ByRefEvent]
public record struct SmokeShieldEvent(EntityUid Target)
{
    /// <summary>
    /// They don't breathe it in, but it still touches them.
    /// </summary>
    public bool BlockInhale;

    /// <summary>
    /// It doesn't reach them at all.
    /// </summary>
    public bool BlockAll;
}
