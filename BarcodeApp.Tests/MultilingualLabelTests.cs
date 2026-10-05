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
