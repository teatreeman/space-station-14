namespace Content.Shared._Starlight.Cargo.Exchange;

/// <summary>
/// Trade pricing, shared so the window can show what a trade will cost before it's made. Orders fill at the quoted
/// price: buyers pay the ask, sellers get the bid, and the fee comes on top. Costs round up and proceeds round down.
/// </summary>
public static class ExchangeMath
{
    /// <summary>
    /// The lowest a price can go.
    /// </summary>
    public const float MinPrice = 1f;

    /// <summary>
    /// The price a buyer pays for one share.
    /// </summary>
    public static float Ask(float price, float spread) => price * (1f + spread / 2f);

    /// <summary>
    /// The price a seller gets for one share.
    /// </summary>
    public static float Bid(float price, float spread) => price * (1f - spread / 2f);

    /// <summary>
    /// What buying <paramref name="shares"/> costs.
    /// </summary>
    public static long Buy(float price, int shares, float spread, float fee)
    {
        return (long) Math.Ceiling((double) Ask(price, spread) * shares * (1.0 + fee));
    }

    /// <summary>
    /// What selling <paramref name="shares"/> pays.
    /// </summary>
    public static long Sell(float price, int shares, float spread, float fee)
    {
        return (long) Math.Floor((double) Bid(price, spread) * shares * (1.0 - fee));
    }

    /// <summary>
    /// The most shares that cost no more than <paramref name="budget"/>.
    /// </summary>
    public static int MaxAffordable(float price, long budget, float spread, float fee)
    {
        if (budget <= 0)
            return 0;

        var shares = (int) Math.Min(int.MaxValue - 1, Math.Floor(budget / ((double) Ask(price, spread) * (1.0 + fee))));
        while (shares > 0 && Buy(price, shares, spread, fee) > budget)
        {
            shares--;
        }

        return shares;
    }
}
