using Content.Shared._Starlight.Time;

namespace Content.Shared._Starlight.Cargo.Exchange;

/// <summary>
/// Every time the exchange shows a player goes through here, so it reads like the station clock on their PDA.
/// </summary>
public static class ExchangeTime
{
    /// <summary>
    /// A game-time instant as station time, hh:mm.
    /// </summary>
    public static string Format(SharedTimeSystem time, TimeSpan curTime)
    {
        return time.GetStationTime(curTime).Time.ToString(@"hh\:mm");
    }
}
