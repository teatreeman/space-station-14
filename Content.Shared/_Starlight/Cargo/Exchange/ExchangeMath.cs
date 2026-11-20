namespace Content.Shared._Starlight.Cargo.Exchange;

/// <summary>
/// Trade pricing, shared so the window can show what a trade will cost before it's made.
/// An order walks the price as it fills, so it pays the average of the price before and after it,
/// plus half the spread and the fee. Costs round up and proceeds round down.
/// </summary>
public static class ExchangeMath
{
    /// <summary>
    /// The lowest a price can go.
    /// </summary>
    public const float MinPrice = 1f;

    /// <summary>
    /// What buying <paramref name="shares"/> costs, and the price afterwards.
    /// </summary>
    public static (int Cost, float NewPrice) Buy(float price, int shares, float liquidity, float spread, float fee)
    {
        var newPrice = price * (1f + shares / liquidity);
        var average = (price + newPrice) / 2f;
        var cost = (int) MathF.Ceiling(average * (1f + spread / 2f) * shares * (1f + fee));
        return (cost, newPrice);
    }

    /// <summary>
    /// What selling <paramref name="shares"/> pays, and the price afterwards.
    /// </summary>
    public static (int Proceeds, float NewPrice) Sell(float price, int shares, float liquidity, float spread, float fee)
    {
        var newPrice = MathF.Max(MinPrice, price * (1f - shares / liquidity));
        var average = (price + newPrice) / 2f;
        var proceeds = (int) MathF.Floor(average * (1f - spread / 2f) * shares * (1f - fee));
        return (proceeds, newPrice);
    }

    /// <summary>
    /// The most shares that cost no more than <paramref name="budget"/>.
    /// </summary>
    public static int MaxAffordable(float price, int budget, float liquidity, float spread, float fee, int limit = 1000)
    {
        int low = 0, high = limit;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (Buy(price, mid, liquidity, spread, fee).Cost <= budget)
                low = mid;
            else
                high = mid - 1;
        }

        return low;
    }
}
