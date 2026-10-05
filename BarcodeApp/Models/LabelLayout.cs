namespace Qivisoft.BarcodeApp.Models;

/// <summary>
/// Visual layout of a printed label.
/// </summary>
public enum LabelLayout
{
    /// <summary>Original layout: up to 2 lines of description above a large barcode.</summary>
    Classic = 0,

    /// <summary>PL / EN product names, bold SKU and a compact barcode (e.g. 60x40 mm).</summary>
    Multilingual = 1
}
