using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Starlight.Cargo.Exchange;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Cargo.Exchange;

/// <summary>
/// The terminal's main chart: candles grouped by the chosen timeframe, a moving average, a volume strip along the
/// bottom and a price scale on the right.
/// </summary>
public sealed class ExchangeCandleChart : Control
{
    private static readonly Color Up = Color.FromHex("#3fd16a");
    private static readonly Color Down = Color.FromHex("#e0504a");
    private static readonly Color Grid = Color.FromHex("#1d2418");
    private static readonly Color Average = Color.FromHex("#ffb000");
    private static readonly Color OpenLine = Color.FromHex("#4a4a32");
    private static readonly Color Scale = Color.FromHex("#9a8a60");

    private readonly Font _font = IoCManager.Resolve<IResourceCache>().GetFont("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf", 10);

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

    private readonly List<ExchangeCandle> _grouped = new();

    public void SetCandles(IReadOnlyList<ExchangeCandle> candles)
    {
        Candles = candles;
        Regroup();
    }

    public void Regroup()
    {
        _grouped.Clear();
        var step = Math.Max(1, Timeframe);
        // Group from the newest candle back, so the last group is always the latest tick.
        var start = Candles.Count % step;
        if (start > 0)
            _grouped.Add(Merge(0, start));
        for (var i = start; i < Candles.Count; i += step)
        {
            _grouped.Add(Merge(i, Math.Min(Candles.Count, i + step)));
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

    private (float Low, float High) Range()
    {
        if (_grouped.Count == 0)
            return (0f, 1f);

        float low = float.MaxValue, high = float.MinValue;
        foreach (var candle in _grouped)
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
        var (low, high) = Range();
        var range = MathF.Max(high - low, 0.0001f);
        float Y(float price) => priceHeight - ((price - low) / range * priceHeight);

        var label = handle.GetDimensions(_font, high.ToString("0.00"), UIScale);
        var width = size.X - label.X - (8f * UIScale);
        void DrawPrice(float price, Color color) => handle.DrawString(_font,
            new Vector2(width + (4f * UIScale), Y(price) - (label.Y / 2f)), price.ToString("0.00"), UIScale, color);

        for (var i = 1; i < 4; i++)
        {
            var y = priceHeight * i / 4f;
            handle.DrawLine(new Vector2(0, y), new Vector2(width, y), Grid);
        }

        handle.DrawLine(new Vector2(0, volumeTop - 2), new Vector2(width, volumeTop - 2), Grid);

        if (_grouped.Count == 0)
            return;

        if (Reference is { } reference)
            handle.DrawLine(new Vector2(0, Y(reference)), new Vector2(width, Y(reference)), OpenLine);

        var slot = width / Math.Max(_grouped.Count, 20);
        var body = MathF.Max(1f, slot * 0.6f);
        var maxVolume = 1f;
        foreach (var candle in _grouped)
        {
            maxVolume = MathF.Max(maxVolume, candle.Volume);
        }

        var offset = width - (slot * _grouped.Count);
        for (var i = 0; i < _grouped.Count; i++)
        {
            var candle = _grouped[i];
            var color = candle.Close >= candle.Open ? Up : Down;
            var x = offset + (slot * (i + 0.5f));

            handle.DrawLine(new Vector2(x, Y(candle.High)), new Vector2(x, Y(candle.Low)), color);
            var top = Y(MathF.Max(candle.Open, candle.Close));
            var bottom = MathF.Max(top + 1f, Y(MathF.Min(candle.Open, candle.Close)));
            handle.DrawRect(new UIBox2(x - (body / 2f), top, x + (body / 2f), bottom), color);

            var barHeight = (size.Y - volumeTop) * candle.Volume / maxVolume;
            handle.DrawRect(new UIBox2(x - (body / 2f), size.Y - barHeight, x + (body / 2f), size.Y), color.WithAlpha(0.5f));
        }

        var latest = _grouped[^1];
        var latestY = Y(latest.Close);
        for (var i = 1; i < 4; i++)
        {
            if (MathF.Abs((priceHeight * i / 4f) - latestY) > label.Y)
                DrawPrice(high - (range * i / 4f), Scale);
        }

        handle.DrawRect(new UIBox2(width, latestY - (label.Y / 2f), size.X, latestY + (label.Y / 2f)), latest.Close >= latest.Open ? Up : Down);
        DrawPrice(latest.Close, Color.Black);

        if (!ShowAverage || _grouped.Count < 2)
            return;

        Vector2? last = null;
        for (var i = 0; i < _grouped.Count; i++)
        {
            var from = Math.Max(0, i - AveragePeriod + 1);
            var sum = 0f;
            for (var j = from; j <= i; j++)
            {
                sum += _grouped[j].Close;
            }

            var point = new Vector2(offset + (slot * (i + 0.5f)), Y(sum / (i - from + 1)));
            if (last is { } previous)
                handle.DrawLine(previous, point, Average);
            last = point;
        }
    }
}
