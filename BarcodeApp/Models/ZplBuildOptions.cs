namespace Qivisoft.BarcodeApp.Models;

public sealed class ZplBuildOptions
{
    public BarcodeSymbology BarcodeType { get; init; } = BarcodeSymbology.Ean13;

    public int BarcodeModuleWidthDots { get; init; } = 3;

    public bool IncludeProductName { get; init; } = true;

    public int MaxProductNameLength { get; init; } = 42;

    public int LabelWidthDots { get; init; } = 600;

    public int BarcodeHeightDots { get; init; } = 110;

    /// <summary>
    /// When > 0 emits ^LL to fix label height. 0 = auto-size (ZPL default).
    /// </summary>
    public int LabelHeightDots { get; init; } = 0;

    public LabelLayout Layout { get; init; } = LabelLayout.Classic;

    /// <summary>Printer resolution, used to scale the multilingual layout.</summary>
    public int PrinterDpi { get; init; } = 203;

    /// <summary>Prints the price as its own line (multilingual layout only).</summary>
    public bool IncludePrice { get; init; } = false;

    public PriceCurrency Currency { get; init; } = PriceCurrency.Pln;

    /// <summary>
    /// When set (multilingual layout), lines with characters outside plain ASCII
    /// (Polish diacritics, €) are rendered as graphics so they print on any Zebra font.
    /// </summary>
    public Services.ITextRasterizer? TextRasterizer { get; init; }
}