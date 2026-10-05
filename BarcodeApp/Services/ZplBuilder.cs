using System.Text;
using Qivisoft.BarcodeApp.Models;

namespace Qivisoft.BarcodeApp.Services;

public static class ZplBuilder
{
    public static string Build(IEnumerable<ValidProductData> rows, ZplBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);

        var builder = new StringBuilder();

        foreach (var row in rows)
        {
            if (options.Layout == LabelLayout.Multilingual)
            {
                var label = BuildMultilingualLabel(row, options);
                for (var i = 0; i < row.Quantity; i++)
                    builder.Append(label);

                continue;
            }

            var safeName = EscapeField(row.Name);
            var barcodeFieldData = BarcodeValueRules.BuildZplFieldData(row.Ean, options.BarcodeType);
            var barcodeCommand = BuildBarcodeCommand(options.BarcodeType, options.BarcodeModuleWidthDots, options.BarcodeHeightDots, barcodeFieldData);
            var barcodeX = CalculateCenteredBarcodeX(options.LabelWidthDots, options.BarcodeType, options.BarcodeModuleWidthDots);

            for (var i = 0; i < row.Quantity; i++)
            {
                builder.AppendLine("^XA");
                builder.AppendLine($"^PW{options.LabelWidthDots}");
                if (options.LabelHeightDots > 0)
                    builder.AppendLine($"^LL{options.LabelHeightDots}");
                builder.AppendLine("^LH0,0");
                // Use UTF-8 code page for Polish diacritics in product names.
                builder.AppendLine("^CI28");

                if (options.IncludeProductName && !string.IsNullOrWhiteSpace(safeName))
                {
                    var nameFieldData = EnsureFieldBlockLineSeparator(safeName);
                    // 2-line centered description using Field Block
                    builder.AppendLine($"^FO0,12^FB{options.LabelWidthDots},2,2,C^A0N,28,28^FD{nameFieldData}^FS");
                    builder.AppendLine($"^FO{barcodeX},82{barcodeCommand}");
                }
                else
                {
                    builder.AppendLine($"^FO{barcodeX},30{barcodeCommand}");
                }

                builder.AppendLine("^XZ");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds one label in the multilingual layout:
    /// PL name, EN name, bold SKU, optional price line and a compact barcode below.
    /// Empty lines are skipped. Long names are condensed (narrower glyphs),
    /// then shrunk, and only as a last resort wrapped onto two lines.
    /// Dimensions are designed for 203 dpi and scaled for other resolutions.
    /// </summary>
    private static string BuildMultilingualLabel(ValidProductData row, ZplBuildOptions options)
    {
        var dpi = options.PrinterDpi > 0 ? options.PrinterDpi : 203;
        var scale = dpi / 203.0;
        int Scaled(double value) => (int)Math.Round(value * scale);

        var labelWidth = options.LabelWidthDots;
        var margin = Scaled(12);
        var textWidth = Math.Max(labelWidth - 2 * margin, 40);

        var builder = new StringBuilder();
        builder.AppendLine("^XA");
        builder.AppendLine($"^PW{labelWidth}");
        if (options.LabelHeightDots > 0)
            builder.AppendLine($"^LL{options.LabelHeightDots}");
        builder.AppendLine("^LH0,0");
        // Use UTF-8 code page for Polish diacritics.
        builder.AppendLine("^CI28");

        var y = Scaled(10);

        if (options.IncludeProductName)
        {
            var namePl = string.IsNullOrWhiteSpace(row.NamePl)
                ? row.Name.Replace("\\&", " ", StringComparison.Ordinal)
                : row.NamePl;

            AppendTextLine(builder, EscapeField(namePl), ref y, Scaled(30), margin, textWidth, Scaled(6));
            AppendTextLine(builder, EscapeField(row.NameEn), ref y, Scaled(26), margin, textWidth, Scaled(4));
        }

        AppendTextLine(builder, EscapeField(row.Sku), ref y, Scaled(34), margin, textWidth, Scaled(6));

        if (options.IncludePrice)
            AppendTextLine(builder, EscapeField(PriceFormatter.Format(row.Price, options.Currency)), ref y, Scaled(30), margin, textWidth, Scaled(6));

        // Never go below ~0.33 mm per bar (EAN nominal size) so retail/warehouse scanners read it reliably.
        var minimumModule = Math.Max(1, (int)Math.Round(0.33 * dpi / 25.4));
        var moduleWidth = Math.Max(Math.Clamp(options.BarcodeModuleWidthDots, 1, 10), minimumModule);
        var interpretationLineHeight = 9 * moduleWidth + Scaled(4);
        var minimumBarcodeHeight = Scaled(40);

        var barcodeHeight = options.BarcodeHeightDots;
        if (options.LabelHeightDots > 0)
        {
            var available = options.LabelHeightDots - y - interpretationLineHeight - Scaled(6);
            barcodeHeight = Math.Clamp(available, minimumBarcodeHeight, Math.Max(options.BarcodeHeightDots, minimumBarcodeHeight));
        }

        var barcodeFieldData = BarcodeValueRules.BuildZplFieldData(row.Ean, options.BarcodeType);
        var barcodeCommand = BuildBarcodeCommand(options.BarcodeType, moduleWidth, barcodeHeight, barcodeFieldData);
        var barcodeX = CalculateCenteredBarcodeX(labelWidth, options.BarcodeType, moduleWidth);
        builder.AppendLine($"^FO{barcodeX},{y}{barcodeCommand}");

        builder.AppendLine("^XZ");
        return builder.ToString();
    }

    private static void AppendTextLine(
        StringBuilder builder,
        string text,
        ref int y,
        int fontHeight,
        int x,
        int textWidth,
        int gapAfter)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var (height, width, lines) = FitText(text, fontHeight, textWidth);
        // Trailing \& keeps ^FB centering reliable on the last line (same convention as the classic layout).
        builder.AppendLine($"^FO{x},{y}^A0N,{height},{width}^FB{textWidth},{lines + 1},0,C^FD{text}\\&^FS");
        y += lines * height + gapAfter;
    }

    /// <summary>
    /// Picks font height/width (dots) and line count so the text fits the given width.
    /// Glyph widths of Zebra font 0 are estimated, so the result is deliberately conservative.
    /// </summary>
    public static (int Height, int Width, int Lines) FitText(string text, int fontHeight, int textWidth)
    {
        var units = EstimateTextUnits(text);
        if (units <= 0)
            return (fontHeight, fontHeight, 1);

        var maxGlyphWidth = (int)Math.Floor(textWidth / units);

        // 1) Fits as is.
        if (maxGlyphWidth >= fontHeight)
            return (fontHeight, fontHeight, 1);

        // 2) Condense glyphs horizontally down to 75% of height.
        var condensedLimit = (int)Math.Ceiling(fontHeight * 0.75);
        if (maxGlyphWidth >= condensedLimit)
            return (fontHeight, maxGlyphWidth, 1);

        // 3) Shrink the font down to 80% of the base size (keeping the 75% condensation).
        var minimumHeight = (int)Math.Round(fontHeight * 0.8);
        var shrunkHeight = (int)Math.Floor(maxGlyphWidth / 0.75);
        if (shrunkHeight >= minimumHeight)
            return (shrunkHeight, maxGlyphWidth, 1);

        // 4) Wrap onto two lines at the minimum size.
        var twoLineGlyphWidth = (int)Math.Floor(2 * textWidth / units);
        var width = Math.Clamp(twoLineGlyphWidth, (int)Math.Ceiling(minimumHeight * 0.75), minimumHeight);
        return (minimumHeight, width, 2);
    }

    private static double EstimateTextUnits(string text)
    {
        var units = 0.0;
        foreach (var ch in text)
        {
            if (ch == ' ')
                units += 0.30;
            else if (char.IsUpper(ch) || char.IsDigit(ch))
                units += 0.62;
            else if (char.IsLetter(ch))
                units += 0.52;
            else
                units += 0.40;
        }

        return units;
    }

    /// <summary>
    /// Calculates the left X origin (dots) to horizontally center a barcode
    /// within the label. Widths are estimated from module width in <c>^BY</c>.
    /// </summary>
    private static int CalculateCenteredBarcodeX(int labelWidthDots, BarcodeSymbology type, int moduleWidthDots)
    {
        var normalizedModuleWidth = Math.Clamp(moduleWidthDots, 1, 10);
        var baseWidthForModuleOne = type switch
        {
            BarcodeSymbology.Ean13   => 114,
            BarcodeSymbology.Ean8    => 82,
            BarcodeSymbology.UpcA    => 114,
            BarcodeSymbology.Code128 => 100,
            _                        => 114
        };
        var barcodeWidth = baseWidthForModuleOne * normalizedModuleWidth;
        return Math.Max((labelWidthDots - barcodeWidth) / 2, 10);
    }

    private static string EscapeField(string value)
    {
        return value
            .Replace("^", string.Empty, StringComparison.Ordinal)
            .Replace("~", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string EnsureFieldBlockLineSeparator(string value)
    {
        return value.EndsWith("\\&", StringComparison.Ordinal)
            ? value
            : $"{value}\\&";
    }

    private static string BuildBarcodeCommand(BarcodeSymbology type, int moduleWidthDots, int height, string fieldData)
    {
        var normalizedModuleWidth = Math.Clamp(moduleWidthDots, 1, 10);

        return type switch
        {
            BarcodeSymbology.Ean13 => $"^BY{normalizedModuleWidth},2,{height}^BEN,{height},Y,N^FD{fieldData}^FS",
            BarcodeSymbology.Ean8 => $"^BY{normalizedModuleWidth},2,{height}^B8N,{height},Y,N^FD{fieldData}^FS",
            BarcodeSymbology.UpcA => $"^BY{normalizedModuleWidth},2,{height}^BUN,{height},Y,N^FD{fieldData}^FS",
            BarcodeSymbology.Code128 => $"^BY{normalizedModuleWidth},2,{height}^BCN,{height},Y,N,N^FD{fieldData}^FS",
            _ => $"^BY{normalizedModuleWidth},2,{height}^BEN,{height},Y,N^FD{fieldData}^FS"
        };
    }
}