using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Cargo.Exchange;

/// <summary>
/// A small price line, drawn next to each company on the watchlist.
/// </summary>
public sealed class ExchangeChart : Control
{
    public IReadOnlyList<float> Points = Array.Empty<float>();

    /// <summary>
    /// Draws a faint line at this price, such as where the company opened.
    /// </summary>
    public float? Reference;

    public Color LineColor = Color.FromHex("#ffb000");

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Points.Count < 2)
            return;

        var min = float.MaxValue;
        var max = float.MinValue;
        foreach (var point in Points)
        {
            min = MathF.Min(min, point);
            max = MathF.Max(max, point);
        }

        if (Reference is { } reference)
        {
            min = MathF.Min(min, reference);
            max = MathF.Max(max, reference);
        }

        // Keep a flat line in the middle instead of dividing by nothing.
        var range = MathF.Max(max - min, max * 0.01f + 0.01f);
        var size = PixelSize;
        Vector2 At(int i, float price) => new(i * (size.X - 1f) / (Points.Count - 1),
            size.Y - 2f - (price - min) / range * (size.Y - 4f));

        if (Reference is { } line)
        {
            var y = At(0, line).Y;
            handle.DrawLine(new Vector2(0, y), new Vector2(size.X, y), Color.FromHex("#3a3a2a"));
        }

        var color = Points[^1] >= Points[0] ? Color.FromHex("#3fd16a") : Color.FromHex("#e0504a");
        for (var i = 1; i < Points.Count; i++)
        {
            handle.DrawLine(At(i - 1, Points[i - 1]), At(i, Points[i]), color);
        }
    }
}
