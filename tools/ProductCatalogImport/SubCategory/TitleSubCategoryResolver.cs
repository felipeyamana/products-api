using System.Text.RegularExpressions;

namespace ProductCatalogImport.SubCategory;

/// <summary>
/// Derives a small set of subcategories from product titles. First matching rule wins.
/// </summary>
public sealed partial class TitleSubCategoryResolver
{
    private readonly string _fallback;

    public TitleSubCategoryResolver(string fallbackName)
    {
        _fallback = fallbackName;
    }

    public string Resolve(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return _fallback;

        var t = title.ToLowerInvariant();

        if (ContainsAny(t, "smart watch", "smartwatch"))
            return "Wearables";

        if (ContainsAny(t, "ipad") || Regex.IsMatch(t, @"\btablet\b"))
            return "Tablets";

        if (ContainsAny(t, "laptop", "macbook", "chromebook"))
            return "Laptops";

        if (ContainsAny(t, " television", " tv ", " tv,", "smart tv", "fire tv"))
            return "TV & Home Theater";

        if (ContainsAny(t, "camera", "dslr", "gopro", "mirrorless"))
            return "Cameras";

        if (ContainsAny(t, "power bank", "adapter", "charger", "usb-c power", "charging cable", "wall charger"))
            return "Power & Charging";

        if (ContainsAny(t, "keyboard", "mouse ", "mouse,", "webcam", "cleaning kit", "cleaning brush"))
            return "Computer Accessories";

        if (ContainsAny(t,
                "airdopes", "earbuds", "earphones", "headphones", "headset",
                "bassheads", "rockerz", "neckband", "truly wireless", "in-ear"))
            return "Audio";

        if (ContainsAny(t,
                "galaxy ", "iphone", "redmi ", "oneplus ", "realme ", "pixel ",
                "oppo ", "vivo ", "poco ", "nothing phone", "smartphone", "mobile phone")
            || Regex.IsMatch(t, @"\b5g\b.*\b(ram|gb|storage|phone)\b"))
            return "Mobile Phones";

        return _fallback;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
