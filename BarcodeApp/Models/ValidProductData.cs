namespace Qivisoft.BarcodeApp.Models;

public sealed class ValidProductData
{
    public required string Ean { get; init; }

    public required string Name { get; init; }

    public required int Quantity { get; init; }

    /// <summary>Raw Polish product name (used by the multilingual layout).</summary>
    public string NamePl { get; init; } = string.Empty;

    public string NameEn { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    /// <summary>Raw price as typed/imported (used by the multilingual layout).</summary>
    public string Price { get; init; } = string.Empty;
}