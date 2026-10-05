using System.Text.RegularExpressions;
using Qivisoft.BarcodeApp.Models;
using Qivisoft.BarcodeApp.Services;
using Qivisoft.BarcodeApp.Tests.TestHelpers;
using Qivisoft.BarcodeApp.ViewModels;

namespace Qivisoft.BarcodeApp.Tests;

public sealed class MultilingualLabelTests
{
    private static ValidProductData Collar(string nameEn = "Braided leather collar S brown", int quantity = 1) => new()
    {
        Ean = "5907053181876",
        Name = "OBROŻA S skóra zaplatana brąz OZPASSBR",
        NamePl = "OBROŻA S skóra zaplatana brąz",
        NameEn = nameEn,
        Sku = "OZPASSBR",
        Quantity = quantity
    };

    private static ZplBuildOptions Options60x40(int dpi = 203) => new()
    {
        Layout = LabelLayout.Multilingual,
        PrinterDpi = dpi,
        LabelWidthDots = dpi == 300 ? 709 : 480,
        LabelHeightDots = dpi == 300 ? 472 : 320,
        BarcodeHeightDots = dpi == 300 ? 165 : 110
    };

    [Fact]
    public void Build_Multilingual_PrintsAllLanguagesSkuAndBarcode()
    {
        var zpl = ZplBuilder.Build([Collar()], Options60x40());

        Assert.Contains("^FDOBROŻA S skóra zaplatana brąz\\&^FS", zpl);
        Assert.Contains("^FDBraided leather collar S brown\\&^FS", zpl);
        Assert.Contains("^FDOZPASSBR\\&^FS", zpl);
        Assert.Contains("^BEN,", zpl);
        Assert.Contains("^FD590705318187^FS", zpl);
        Assert.Contains("^PW480", zpl);
        Assert.Contains("^LL320", zpl);
        Assert.Contains("^CI28", zpl);
        // Classic layout puts SKU into the name line; multilingual must not.
        Assert.DoesNotContain("brąz OZPASSBR", zpl);
    }

    [Fact]
    public void Build_Multilingual_BarcodeFitsInsideLabelHeight()
    {
        var zpl = ZplBuilder.Build([Collar()], Options60x40());

        var match = Regex.Match(zpl, @"\^FO\d+,(?<y>\d+)\^BY(?<m>\d+),2,(?<h>\d+)\^BEN");
        Assert.True(match.Success);

        var y = int.Parse(match.Groups["y"].Value);
        var height = int.Parse(match.Groups["h"].Value);
        var module = int.Parse(match.Groups["m"].Value);

        Assert.True(height <= 110, $"Barcode height {height} should not exceed the configured maximum.");
        Assert.True(y + height + 9 * module <= 320, $"Barcode (y={y}, h={height}) does not fit on a 40 mm label.");
    }

    [Fact]
    public void Build_Multilingual_SkipsEmptyTranslation()
    {
        var zpl = ZplBuilder.Build([Collar(nameEn: " ")], Options60x40());

        // Only PL name + SKU text fields.
        Assert.Equal(2, Regex.Matches(zpl, @"\^A0N").Count);
    }

    [Fact]
    public void Build_Multilingual_RepeatsLabelPerQuantity()
    {
        var zpl = ZplBuilder.Build([Collar(quantity: 4)], Options60x40());

        Assert.Equal(4, Regex.Matches(zpl, @"\^XA").Count);
        Assert.Equal(4, Regex.Matches(zpl, @"\^XZ").Count);
    }

    [Fact]
    public void Build_Multilingual_KeepsScannableModuleWidthAt300Dpi()
    {
        var zpl = ZplBuilder.Build([Collar()], Options60x40(300));

        Assert.Contains("^BY4,2,", zpl);
        Assert.Contains("^PW709", zpl);
    }

    [Fact]
    public void Build_Multilingual_HidesNames_WhenIncludeProductNameIsOff()
    {
        var options = new ZplBuildOptions
        {
            Layout = LabelLayout.Multilingual,
            IncludeProductName = false,
            LabelWidthDots = 480,
            LabelHeightDots = 320
        };

        var zpl = ZplBuilder.Build([Collar()], options);

        Assert.DoesNotContain("Braided", zpl);
        Assert.Contains("^FDOZPASSBR\\&^FS", zpl);
    }

    [Fact]
    public void FitText_KeepsShortText_AtBaseSize()
    {
        Assert.Equal((30, 30, 1), ZplBuilder.FitText("OZPASSBR", 30, 456));
    }

    [Fact]
    public void FitText_CondensesOrShrinks_LongerText_OnOneLine()
    {
        var (height, width, lines) = ZplBuilder.FitText(
            "Geflochtene Leder Hundehalsband S braun mit Messingschnalle", 22, 456);

        Assert.Equal(1, lines);
        Assert.True(width < 22);
        Assert.True(height <= 22);
    }

    [Fact]
    public void FitText_WrapsVeryLongText_OnTwoLines()
    {
        var text = string.Join(' ', Enumerable.Repeat("Hundehalsband", 10));

        var (_, _, lines) = ZplBuilder.FitText(text, 22, 456);

        Assert.Equal(2, lines);
    }

    [Fact]
    public void Import_Csv_ReadsEnglishNameColumn()
    {
        using var temp = new TempPath();
        var path = temp.GetFilePath("multilingual.csv");

        File.WriteAllText(path,
            "EAN;Nazwa;Nazwa EN;SKU;Ilość\n" +
            "5907053181876;OBROŻA S skóra zaplatana brąz;Braided leather collar S brown;OZPASSBR;2\n");

        var result = new ImportService().Import(path);

        var row = Assert.Single(result.Rows);
        Assert.Equal("OBROŻA S skóra zaplatana brąz", row.Name);
        Assert.Equal("Braided leather collar S brown", row.NameEn);
        Assert.Equal("OZPASSBR", row.Sku);
        Assert.Equal("2", row.QuantityText);
    }

    [Fact]
    public void ProductRow_PassesTranslationsAndSku_ToValidData()
    {
        var row = ProductRowViewModel.FromInput(new ProductInputRow
        {
            Ean = "5907053181876",
            Name = "OBROŻA S skóra zaplatana brąz",
            NameEn = "Braided leather collar S brown",
            Sku = "OZPASSBR",
            QuantityText = "1",
            SourceRowNumber = 2
        });

        Assert.True(row.TryBuildValidData(out var data));
        Assert.Equal("OBROŻA S skóra zaplatana brąz", data.NamePl);
        Assert.Equal("Braided leather collar S brown", data.NameEn);
        Assert.Equal("OZPASSBR", data.Sku);
    }

    [Fact]
    public void StickerPreview_Multilingual_ShowsAllLines()
    {
        var row = new ProductRowViewModel
        {
            Ean = "5907053181876",
            Name = "OBROŻA S skóra zaplatana brąz",
            NameEn = "Braided leather collar S brown",
            Sku = "OZPASSBR",
            QuantityText = "1"
        };

        var vm = new StickerPreviewViewModel(row, true, BarcodeSymbology.Ean13, LabelLayout.Multilingual);

        Assert.Equal("OBROŻA S skóra zaplatana brąz", vm.DescriptionLine1);
        Assert.Equal("Braided leather collar S brown", vm.DescriptionLine2);
        Assert.Equal("OZPASSBR", vm.SkuLine);
    }
}

public sealed class PriceLineTests
{
    private static ValidProductData Collar(string price) => new()
    {
        Ean = "5907053181876",
        Name = "OBROŻA S skóra zaplatana brąz",
        NamePl = "OBROŻA S skóra zaplatana brąz",
        NameEn = "Braided leather collar S brown",
        Sku = "OZPASSBR",
        Price = price,
        Quantity = 1
    };

    private static ZplBuildOptions Options(bool includePrice, PriceCurrency currency = PriceCurrency.Pln) => new()
    {
        Layout = LabelLayout.Multilingual,
        LabelWidthDots = 480,
        LabelHeightDots = 320,
        IncludePrice = includePrice,
        Currency = currency
    };

    [Theory]
    [InlineData("49.9", PriceCurrency.Pln, "49,90 zł")]
    [InlineData("49,90 zł", PriceCurrency.Pln, "49,90 zł")]
    [InlineData("1234,5", PriceCurrency.Pln, "1 234,50 zł")]
    [InlineData("1.234,50", PriceCurrency.Eur, "1 234,50 €")]
    [InlineData("12 EUR", PriceCurrency.Eur, "12,00 €")]
    [InlineData("", PriceCurrency.Pln, "")]
    public void PriceFormatter_FormatsPolishStyle(string raw, PriceCurrency currency, string expected)
    {
        Assert.Equal(expected, PriceFormatter.Format(raw, currency));
    }

    [Fact]
    public void Build_PrintsPriceLine_WhenEnabled()
    {
        var zpl = ZplBuilder.Build([Collar("59,9")], Options(true, PriceCurrency.Eur));

        Assert.Contains("^FD59,90 €\\&^FS", zpl);
    }

    [Fact]
    public void Build_OmitsPriceLine_WhenDisabled()
    {
        var zpl = ZplBuilder.Build([Collar("59,9")], Options(false));

        Assert.DoesNotContain("59,90", zpl);
    }

    [Fact]
    public void Build_WithPrice_BarcodeStillFitsOn40mmLabel()
    {
        var zpl = ZplBuilder.Build([Collar("59,90")], Options(true));

        var match = System.Text.RegularExpressions.Regex.Match(zpl, @"\^FO\d+,(?<y>\d+)\^BY(?<m>\d+),2,(?<h>\d+)\^BEN");
        Assert.True(match.Success);
        var bottom = int.Parse(match.Groups["y"].Value) + int.Parse(match.Groups["h"].Value) + 9 * int.Parse(match.Groups["m"].Value);
        Assert.True(bottom <= 320, $"Barcode bottom at {bottom} dots exceeds the 40 mm label.");
    }
}

public sealed class PolishCharactersTests
{
    private sealed class FakeRasterizer : ITextRasterizer
    {
        public List<string> Rendered { get; } = [];

        public float BaseScaleX => 1f;

        public float Measure(string text, int fontHeight, float scaleX) => text.Length * fontHeight * 0.5f * scaleX;

        public MonoBitmap Render(IReadOnlyList<string> lines, int width, int fontHeight, float scaleX, out int capTopOffset)
        {
            Rendered.AddRange(lines);
            capTopOffset = 3;
            var bytesPerRow = (width + 7) / 8;
            return new MonoBitmap(width, fontHeight, bytesPerRow, new byte[bytesPerRow * fontHeight]);
        }
    }

    private static ValidProductData Collar() => new()
    {
        Ean = "5907053181876",
        Name = "OBROŻA S skóra zaplatana brąz",
        NamePl = "OBROŻA S skóra zaplatana brąz",
        NameEn = "Braided leather collar S brown",
        Sku = "OZPASSBR",
        Quantity = 1
    };

    [Fact]
    public void Build_RendersOnlyLinesWithPolishCharacters_AsGraphics()
    {
        var rasterizer = new FakeRasterizer();
        var zpl = ZplBuilder.Build([Collar()], new ZplBuildOptions
        {
            Layout = LabelLayout.Multilingual,
            LabelWidthDots = 480,
            LabelHeightDots = 320,
            TextRasterizer = rasterizer
        });

        Assert.Equal(["OBROŻA S skóra zaplatana brąz"], rasterizer.Rendered);
        Assert.Contains("^GFA,", zpl);
        Assert.DoesNotContain("^FDOBROŻA", zpl);
        // ASCII lines stay in the printer font.
        Assert.Contains("^FDBraided leather collar S brown\\&^FS", zpl);
        Assert.Contains("^FDOZPASSBR\\&^FS", zpl);
    }

    [Fact]
    public void Build_WithoutRasterizer_KeepsTextFields()
    {
        var zpl = ZplBuilder.Build([Collar()], new ZplBuildOptions
        {
            Layout = LabelLayout.Multilingual,
            LabelWidthDots = 480,
            LabelHeightDots = 320
        });

        Assert.DoesNotContain("^GFA", zpl);
    }

    [Theory]
    [InlineData("OZPASSBR", false)]
    [InlineData("Braided leather collar S brown", false)]
    [InlineData("OBROŻA", true)]
    [InlineData("BRĄZ", true)]
    [InlineData("59,90 zł", true)]
    [InlineData("59,90 €", true)]
    public void NeedsGraphic_DetectsNonAsciiCharacters(string text, bool expected)
    {
        Assert.Equal(expected, ZplBuilder.NeedsGraphic(text));
    }

    [Fact]
    public void ToGraphicField_UsesZplGfaFormat()
    {
        var bitmap = new MonoBitmap(16, 2, 2, [0xFF, 0x00, 0x0F, 0xF0]);

        Assert.Equal("^GFA,4,4,2,FF000FF0", ZplBuilder.ToGraphicField(bitmap));
    }

    [Fact]
    public void SkiaRasterizer_DrawsPolishText()
    {
        var rasterizer = new SkiaTextRasterizer();

        var bitmap = rasterizer.Render(["OBROŻA BRĄZ"], 456, 30, rasterizer.BaseScaleX, out var capTop);

        Assert.Equal(57, bitmap.BytesPerRow);
        Assert.True(bitmap.Height >= 30);
        Assert.True(capTop >= 0);
        Assert.Contains(bitmap.Data, b => b != 0);
        Assert.True(rasterizer.Measure("OBROŻA BRĄZ", 30, rasterizer.BaseScaleX) > 0);
    }
}
