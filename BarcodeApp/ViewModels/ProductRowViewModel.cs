using Qivisoft.BarcodeApp.Models;
using Qivisoft.BarcodeApp.Services;

namespace Qivisoft.BarcodeApp.ViewModels;

public sealed class ProductRowViewModel : ViewModelBase
{
    private const int PreviewLineLength = 24;

    public BarcodeSymbology BarcodeType
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                Validate();
                NotifyPreviewChanged();
            }
        }
    } = BarcodeSymbology.Ean13;

    public string Ean
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                Validate();
                NotifyPreviewChanged();
            }
        }
    } = string.Empty;

    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                Validate();
                NotifyPreviewChanged();
            }
        }
    } = string.Empty;

    public string QuantityText
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) Validate();
        }
    } = "1";

    public string Sku
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                NotifyPreviewChanged();
                RowChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    } = string.Empty;

    public string Price
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                NotifyPreviewChanged();
                RowChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    } = string.Empty;

    public string NameEn
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                RowChanged?.Invoke(this, EventArgs.Empty);
        }
    } = string.Empty;

    public string PreviewDescriptionLine1 => BuildPreviewLines(Sku, Name, Price).line1;

    public string PreviewDescriptionLine2 => BuildPreviewLines(Sku, Name, Price).line2;

    public string PreviewBarcodeText => BuildPreviewBarcodeText(Ean);

    public string PreviewBarcodeBars => BuildPreviewBarcodeBars(Ean);

    public bool IsValid
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string ValidationMessage
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public event EventHandler? RowChanged;

    public static ProductRowViewModel CreateEmpty()
    {
        var row = new ProductRowViewModel();
        row.Validate();
        return row;
    }

    public static ProductRowViewModel FromInput(ProductInputRow input)
    {
        return new ProductRowViewModel
        {
            Ean = input.Ean,
            Name = input.Name,
            QuantityText = input.QuantityText,
            Sku = input.Sku,
            Price = input.Price,
            NameEn = input.NameEn,
            BarcodeType = BarcodeSymbology.Ean13
        };
    }

    public bool TryBuildValidData(out ValidProductData data)
    {
        Validate();

        if (!IsValid)
        {
            data = null!;
            return false;
        }

        data = new ValidProductData
        {
            Ean = Ean.Trim(),
            Name = BuildDescription(Sku, Name, Price),
            Quantity = int.Parse(QuantityText.Trim()),
            NamePl = Name.Trim(),
            NameEn = NameEn.Trim(),
            Sku = Sku.Trim(),
            Price = Price.Trim()
        };

        return true;
    }

    public void Validate()
    {
        var errors = new List<string>();
        var normalizedEan = Ean.Trim();

        if (!BarcodeValueRules.IsValid(normalizedEan, BarcodeType))
            errors.Add(BarcodeValueRules.ValidationMessage(BarcodeType));

        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Nazwa produktu jest wymagana.");

        if (!int.TryParse(QuantityText.Trim(), out var qty) || qty <= 0)
            errors.Add("Ilość musi być dodatnią liczbą całkowitą.");

        IsValid = errors.Count == 0;
        ValidationMessage = IsValid ? "OK" : string.Join(" | ", errors);
        RowChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildDescription(string sku, string name, string price)
    {
        var normalizedName = name.Trim();
        var extraInfo = new[] { sku, price }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim());
        var normalizedExtra = string.Join(" ", extraInfo);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return normalizedExtra;

        if (string.IsNullOrWhiteSpace(normalizedExtra))
            return normalizedName;

        var singleLine = $"{normalizedName} {normalizedExtra}";
        if (singleLine.Length <= PreviewLineLength)
            return singleLine;

        // ZPL line break marker for ^FB; used only when the full description is too long.
        return $"{normalizedName}\\&{normalizedExtra}";
    }

    private static (string line1, string line2) BuildPreviewLines(string sku, string name, string price)
    {
        var composed = BuildDescription(sku, name, price);
        var zplBreak = composed.IndexOf("\\&", StringComparison.Ordinal);
        if (zplBreak >= 0)
        {
            var line1 = composed[..zplBreak].Trim();
            var line2 = composed[(zplBreak + 2)..].Trim();
            return (line1, line2);
        }

        return SplitForPreview(composed);
    }

    private static (string line1, string line2) SplitForPreview(string text)
    {
        var normalized = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
            return (string.Empty, string.Empty);

        if (normalized.Length <= PreviewLineLength)
            return (normalized, string.Empty);

        var firstBreak = normalized.LastIndexOf(' ', PreviewLineLength);
        if (firstBreak < 1)
            firstBreak = PreviewLineLength;

        var line1 = normalized[..firstBreak].Trim();
        var remainder = normalized[firstBreak..].TrimStart();

        if (remainder.Length <= PreviewLineLength)
            return (line1, remainder);

        var secondBreak = remainder.LastIndexOf(' ', PreviewLineLength);
        if (secondBreak < 1)
            secondBreak = PreviewLineLength;

        var line2 = remainder[..secondBreak].TrimEnd();
        if (secondBreak < remainder.Length)
            line2 += "...";

        return (line1, line2);
    }

    private static string BuildPreviewBarcodeText(string ean)
    {
        var value = ean.Trim();
        if (string.IsNullOrEmpty(value))
            return "(brak kodu)";

        return string.Join(' ', value.Chunk(1).Select(chars => new string(chars)));
    }

    private static string BuildPreviewBarcodeBars(string ean)
    {
        var digits = ean.Trim();
        if (string.IsNullOrEmpty(digits))
            return "||||||||||||||||||||";

        var bars = new System.Text.StringBuilder();
        for (var index = 0; index < digits.Length; index++)
        {
            var ch = digits[index];
            var width = char.IsDigit(ch) ? ((ch - '0') % 3) + 1 : 2;
            bars.Append(new string('|', width));

            // Keep subtle separators every two symbols to suggest scanner grouping.
            if (index % 2 == 1)
                bars.Append(' ');
        }

        return bars.ToString();
    }

    private void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(PreviewDescriptionLine1));
        OnPropertyChanged(nameof(PreviewDescriptionLine2));
        OnPropertyChanged(nameof(PreviewBarcodeText));
        OnPropertyChanged(nameof(PreviewBarcodeBars));
    }
}