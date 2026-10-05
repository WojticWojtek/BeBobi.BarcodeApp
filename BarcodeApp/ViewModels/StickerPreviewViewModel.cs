using Qivisoft.BarcodeApp.Models;
using Qivisoft.BarcodeApp.Services;

namespace Qivisoft.BarcodeApp.ViewModels;

public sealed class StickerPreviewViewModel : ViewModelBase
{
    public StickerPreviewViewModel(
        ProductRowViewModel row,
        bool includeProductName,
        BarcodeSymbology barcodeType,
        LabelLayout layout = LabelLayout.Classic,
        bool includePrice = false,
        PriceCurrency currency = PriceCurrency.Pln)
    {
        ArgumentNullException.ThrowIfNull(row);

        IncludeProductName = includeProductName;
        BarcodeType = barcodeType;
        Layout = layout;

        if (layout == LabelLayout.Multilingual)
        {
            DescriptionLine1 = includeProductName ? row.Name.Trim() : string.Empty;
            DescriptionLine2 = includeProductName ? row.NameEn.Trim() : string.Empty;
            SkuLine = row.Sku.Trim();
            PriceLine = includePrice ? PriceFormatter.Format(row.Price, currency) : string.Empty;
        }
        else
        {
            DescriptionLine1 = includeProductName ? row.PreviewDescriptionLine1 : string.Empty;
            DescriptionLine2 = includeProductName ? row.PreviewDescriptionLine2 : string.Empty;
        }

        BarcodeBars = row.PreviewBarcodeBars;
        BarcodeText = row.PreviewBarcodeText;
        ValidationMessage = row.ValidationMessage;
        IsValid = row.IsValid;
    }

    public bool IncludeProductName { get; }

    public BarcodeSymbology BarcodeType { get; }

    public string DescriptionLine1 { get; }

    public string DescriptionLine2 { get; }

    public LabelLayout Layout { get; }

    /// <summary>Bold SKU line (multilingual layout only).</summary>
    public string SkuLine { get; } = string.Empty;

    /// <summary>Formatted price line (multilingual layout only).</summary>
    public string PriceLine { get; } = string.Empty;

    public string BarcodeBars { get; }

    public string BarcodeText { get; }

    public string ValidationMessage { get; }

    public bool IsValid { get; }

    public string BarcodeCaption => BarcodeType switch
    {
        BarcodeSymbology.Ean13 => "EAN-13",
        BarcodeSymbology.Ean8 => "EAN-8",
        BarcodeSymbology.UpcA => "UPC-A",
        BarcodeSymbology.Code128 => "Code 128",
        _ => "Kod kreskowy"
    };
}
