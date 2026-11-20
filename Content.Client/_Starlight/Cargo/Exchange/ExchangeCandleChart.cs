using System.Numerics;
using Content.Shared._Starlight.Cargo.Exchange;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Cargo.Exchange;

/// <summary>
/// The terminal's main chart: candles grouped by the chosen timeframe, a moving average, and a volume strip along the
/// bottom.
/// </summary>
public sealed class ExchangeCandleChart : Control
{
    private static readonly Color Up = Color.FromHex("#3fd16a");
    private static readonly Color Down = Color.FromHex("#e0504a");
    private static readonly Color Grid = Color.FromHex("#1d2418");
    private static readonly Color Average = Color.FromHex("#ffb000");
    private static readonly Color OpenLine = Color.FromHex("#4a4a32");

    /// <summary>
    /// Ticks per candle.
    /// </summary>
    public int Timeframe = 1;

    public bool ShowAverage = true;

    /// <summary>
    /// Candles in the moving average.
    /// </summary>
    public int AveragePeriod = 10;

    /// <summary>
    /// The company's opening price, drawn as a faint line.
    /// </summary>
    public float? Reference;

    public IReadOnlyList<ExchangeCandle> Candles = Array.Empty<ExchangeCandle>();

    /// <summary>
    /// The candles as drawn, after grouping, so labels can follow them.
    /// </summary>
    public List<ExchangeCandle> Grouped { get; } = new();

    public void SetCandles(IReadOnlyList<ExchangeCandle> candles)
    {
        Candles = candles;
        Regroup();
    }

    public void Regroup()
    {
        Grouped.Clear();
        var step = Math.Max(1, Timeframe);
        // Group from the newest candle back, so the last group is always the latest tick.
        var start = Candles.Count % step;
        if (start > 0)
            Grouped.Add(Merge(0, start));
        for (var i = start; i < Candles.Count; i += step)
        {
            Grouped.Add(Merge(i, Math.Min(Candles.Count, i + step)));
        }
    }

    private ExchangeCandle Merge(int from, int to)
    {
        var first = Candles[from];
        float high = first.High, low = first.Low, volume = 0f;
        for (var i = from; i < to; i++)
        {
            high = MathF.Max(high, Candles[i].High);
            low = MathF.Min(low, Candles[i].Low);
            volume += Candles[i].Volume;
        }

        return new ExchangeCandle(first.Open, high, low, Candles[to - 1].Close, volume);
    }

    /// <summary>
    /// Lowest and highest prices on screen.
    /// </summary>
    public (float Low, float High) Range()
    {
        if (Grouped.Count == 0)
            return (0f, 1f);

        float low = float.MaxValue, high = float.MinValue;
        foreach (var candle in Grouped)
        {
            low = MathF.Min(low, candle.Low);
            high = MathF.Max(high, candle.High);
        }

        if (Reference is { } reference)
        {
            low = MathF.Min(low, reference);
            high = MathF.Max(high, reference);
        }

        var pad = MathF.Max((high - low) * 0.05f, high * 0.005f);
        return (low - pad, high + pad);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var size = PixelSize;
        var priceHeight = size.Y * 0.78f;
        var volumeTop = size.Y * 0.82f;

        for (var i = 1; i < 4; i++)
        {
            var y = priceHeight * i / 4f;
            handle.DrawLine(new Vector2(0, y), new Vector2(size.X, y), Grid);
        }

        handle.DrawLine(new Vector2(0, volumeTop - 2), new Vector2(size.X, volumeTop - 2), Grid);

        if (Grouped.Count == 0)
            return;

        var (low, high) = Range();
        var range = MathF.Max(high - low, 0.0001f);
        float Y(float price) => priceHeight - (price - low) / range * priceHeight;

        if (Reference is { } reference)
            handle.DrawLine(new Vector2(0, Y(reference)), new Vector2(size.X, Y(reference)), OpenLine);

        var slot = size.X / (float) Math.Max(Grouped.Count, 20);
        var body = MathF.Max(1f, slot * 0.6f);
        var maxVolume = 1f;
        foreach (var candle in Grouped)
        {
            maxVolume = MathF.Max(maxVolume, candle.Volume);
        }

        var offset = size.X - slot * Grouped.Count;
        for (var i = 0; i < Grouped.Count; i++)
        {
            var candle = Grouped[i];
            var color = candle.Close >= candle.Open ? Up : Down;
            var x = offset + slot * (i + 0.5f);

            handle.DrawLine(new Vector2(x, Y(candle.High)), new Vector2(x, Y(candle.Low)), color);
            var top = Y(MathF.Max(candle.Open, candle.Close));
            var bottom = MathF.Max(top + 1f, Y(MathF.Min(candle.Open, candle.Close)));
            handle.DrawRect(new UIBox2(x - body / 2f, top, x + body / 2f, bottom), color);

            var barHeight = (size.Y - volumeTop) * candle.Volume / maxVolume;
            handle.DrawRect(new UIBox2(x - body / 2f, size.Y - barHeight, x + body / 2f, size.Y), color.WithAlpha(0.5f));
        }

        if (!ShowAverage || Grouped.Count < 2)
            return;

        Vector2? last = null;
        for (var i = 0; i < Grouped.Count; i++)
        {
            var from = Math.Max(0, i - AveragePeriod + 1);
            var sum = 0f;
            for (var j = from; j <= i; j++)
            {
                sum += Grouped[j].Close;
            }

            var point = new Vector2(offset + slot * (i + 0.5f), Y(sum / (i - from + 1)));
            if (last is { } previous)
                handle.DrawLine(previous, point, Average);
            last = point;
        }
    }
}
