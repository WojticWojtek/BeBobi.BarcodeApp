namespace Qivisoft.BarcodeApp.Models;

public sealed class ProductInputRow
{
    public required string Ean { get; init; }

    public required string Name { get; init; }

    public required string QuantityText { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Price { get; init; } = string.Empty;

    public string NameEn { get; init; } = string.Empty;

    public required int SourceRowNumber { get; init; }
}