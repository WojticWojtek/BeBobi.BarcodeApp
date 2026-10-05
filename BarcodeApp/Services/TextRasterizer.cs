using SkiaSharp;

namespace Qivisoft.BarcodeApp.Services;

/// <summary>Monochrome bitmap packed 8 pixels per byte, row-major; bit = 1 means black.</summary>
public sealed record MonoBitmap(int Width, int Height, int BytesPerRow, byte[] Data);

/// <summary>
/// Renders text to a monochrome bitmap. Used for label lines with characters the printer's
/// built-in font cannot print (Polish Ą, Ż, Ś ...), which are then sent as a ZPL graphic.
/// </summary>
public interface ITextRasterizer
{
    /// <summary>Default horizontal scale so the font roughly matches the printer's condensed font.</summary>
    float BaseScaleX { get; }

    float Measure(string text, int fontHeight, float scaleX);

    /// <summary>
    /// Renders one or more lines centered in <paramref name="width"/>.
    /// <paramref name="capTopOffset"/> is the distance from the bitmap top to the top of capital letters
    /// (space above it holds accents), so callers can align caps with neighbouring printer-font text.
    /// </summary>
    MonoBitmap Render(IReadOnlyList<string> lines, int width, int fontHeight, float scaleX, out int capTopOffset);
}

public sealed class SkiaTextRasterizer : ITextRasterizer
{
    private static readonly string[] PreferredFamilies =
    [
        "Arial Narrow", "Arial", "Liberation Sans Narrow", "Liberation Sans", "DejaVu Sans Condensed", "DejaVu Sans"
    ];

    private static readonly Lazy<SKTypeface> Typeface = new(CreateTypeface);

    public float BaseScaleX =>
        Typeface.Value.FamilyName.Contains("Narrow", StringComparison.OrdinalIgnoreCase) ||
        Typeface.Value.FamilyName.Contains("Condensed", StringComparison.OrdinalIgnoreCase)
            ? 1.0f
            : 0.82f;

    public float Measure(string text, int fontHeight, float scaleX)
    {
        using var font = CreateFont(fontHeight, scaleX);
        return font.MeasureText(text);
    }

    public MonoBitmap Render(IReadOnlyList<string> lines, int width, int fontHeight, float scaleX, out int capTopOffset)
    {
        using var font = CreateFont(fontHeight, scaleX);
        font.GetFontMetrics(out var metrics);

        var ascent = (int)Math.Ceiling(-metrics.Ascent);
        var descent = (int)Math.Ceiling(metrics.Descent);
        var capHeight = metrics.CapHeight > 0 ? metrics.CapHeight : fontHeight * 0.72f;
        capTopOffset = Math.Max(0, (int)Math.Floor(ascent - capHeight));

        var height = Math.Max(1, (lines.Count - 1) * fontHeight + ascent + descent);
        width = Math.Max(1, width);

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
        {
            canvas.Clear(SKColors.White);
            for (var i = 0; i < lines.Count; i++)
                canvas.DrawText(lines[i], width / 2f, ascent + i * fontHeight, SKTextAlign.Center, font, paint);
        }

        var bytesPerRow = (width + 7) / 8;
        var data = new byte[bytesPerRow * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var c = bitmap.GetPixel(x, y);
            var luminance = (c.Red * 299 + c.Green * 587 + c.Blue * 114) / 1000;
            if (luminance < 140)
                data[y * bytesPerRow + x / 8] |= (byte)(0x80 >> (x % 8));
        }

        return new MonoBitmap(width, height, bytesPerRow, data);
    }

    private static SKFont CreateFont(int fontHeight, float scaleX) => new(Typeface.Value, fontHeight)
    {
        ScaleX = scaleX,
        Edging = SKFontEdging.Antialias,
        Subpixel = true
    };

    private static SKTypeface CreateTypeface()
    {
        foreach (var family in PreferredFamilies)
        {
            var typeface = SKTypeface.FromFamilyName(family, SKFontStyle.Bold);
            if (typeface is not null && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                return typeface;
        }

        return SKTypeface.FromFamilyName(null, SKFontStyle.Bold) ?? SKTypeface.Default;
    }
}
