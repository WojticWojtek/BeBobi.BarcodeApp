namespace Qivisoft.BarcodeApp.Models;

public sealed class AppUserSettings
{
    public bool IncludeProductName { get; set; } = true;

    public LabelLayout LabelLayout { get; set; } = LabelLayout.Classic;

    public BarcodeSymbology SelectedBarcodeType { get; set; } = BarcodeSymbology.Ean13;

    public string ActivePrinterProfileName { get; set; } = "Domyslny";

    public List<PrinterProfileSettings> PrinterProfiles { get; set; } =
    [
        new()
        {
            Name = "Domyslny",
            BarcodeType = BarcodeSymbology.Ean13,
            PrinterQueueName = string.Empty,
            PrinterHost = string.Empty,
            PrinterPort = 9100,
            PrinterDpi = 203,
            LabelWidthDotsText = "600",
            BarcodeHeightDotsText = "110",
            LabelHeightDotsText = "0"
        }
    ];

    // Legacy properties are kept to migrate existing settings files.
    public int PrinterDpi { get; set; } = 203;
    public string LabelWidthDotsText { get; set; } = "600";
    public string BarcodeHeightDotsText { get; set; } = "110";
    public string LabelHeightDotsText { get; set; } = "0";
}
