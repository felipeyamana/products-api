using System.Globalization;
using System.Text.RegularExpressions;

namespace ProductCatalogImport.Pricing;

public static partial class InrMoneyParser
{
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>Parses values like '₹10,999' or '10,999' to a decimal INR amount.</summary>
    public static bool TryParseInr(string? raw, out decimal inr)
    {
        inr = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var cleaned = Whitespace().Replace(
            raw.Trim().Replace("₹", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal),
            "");

        if (string.IsNullOrEmpty(cleaned))
            return false;

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out inr);
    }

    public static decimal? ToUsd(decimal? inrAmount, decimal inrPerUsd)
    {
        if (inrAmount is null || inrPerUsd <= 0)
            return null;

        var usd = inrAmount.Value / inrPerUsd;
        return Math.Round(usd, 2, MidpointRounding.AwayFromZero);
    }
}
