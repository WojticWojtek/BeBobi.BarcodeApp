using System.Globalization;
using Qivisoft.BarcodeApp.Models;

namespace Qivisoft.BarcodeApp.Services;

/// <summary>
/// Formats a price typed by the user ("49.9", "49,90 zł", "1 234,50") in Polish style
/// with the selected currency: "49,90 zł" or "49,90 €".
/// </summary>
public static class PriceFormatter
{
    public static string Format(string? rawPrice, PriceCurrency currency)
    {
        if (string.IsNullOrWhiteSpace(rawPrice))
            return string.Empty;

        var suffix = currency == PriceCurrency.Eur ? "€" : "zł";

        if (!TryParseAmount(rawPrice, out var amount))
            return $"{StripCurrency(rawPrice)} {suffix}".Trim();

        // "#,##0.00" in invariant culture gives "1,234.50" -> convert to Polish "1 234,50".
        var formatted = amount.ToString("#,##0.00", CultureInfo.InvariantCulture)
            .Replace(",", " ", StringComparison.Ordinal)
            .Replace(".", ",", StringComparison.Ordinal);

        return $"{formatted} {suffix}";
    }

    public static bool TryParseAmount(string? rawPrice, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(rawPrice))
            return false;

        var text = new string(StripCurrency(rawPrice).Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        if (text.Length == 0)
            return false;

        var lastComma = text.LastIndexOf(',');
        var lastDot = text.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Both separators: the last one is the decimal separator, the other groups thousands.
            text = lastComma > lastDot
                ? text.Replace(".", string.Empty, StringComparison.Ordinal)
                : text.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        text = text.Replace(',', '.');

        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                   CultureInfo.InvariantCulture, out amount)
               && amount >= 0;
    }

    private static string StripCurrency(string value)
    {
        var text = value.Trim();
        foreach (var token in new[] { "PLN", "pln", "zł", "ZŁ", "zl", "ZL", "EUR", "eur", "€" })
            text = text.Replace(token, string.Empty, StringComparison.Ordinal);

        return text.Trim();
    }
}
