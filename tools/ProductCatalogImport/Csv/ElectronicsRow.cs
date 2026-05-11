namespace ProductCatalogImport.Csv;

/// <summary>Maps columns from the "All Electronics" style dataset (Amazon India export).</summary>
public sealed class ElectronicsRow
{
    public string Name { get; set; } = "";

    public string MainCategory { get; set; } = "";

    public string SubCategory { get; set; } = "";

    /// <summary>Ignored for persistence (images not imported).</summary>
    public string Image { get; set; } = "";

    public string Link { get; set; } = "";

    public string Ratings { get; set; } = "";

    public string NoOfRatings { get; set; } = "";

    public string DiscountPrice { get; set; } = "";

    public string ActualPrice { get; set; } = "";
}
