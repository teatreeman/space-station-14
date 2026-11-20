namespace Content.Server._Starlight.Chemistry.Events;

/// <summary>
/// Raised on a smoke tile before it touches and gets breathed in by someone standing in it. Cancel it to leave that
/// person alone this time.
/// </summary>
[ByRefEvent]
public record struct SmokeReactAttemptEvent(EntityUid Target, bool Cancelled = false);
