namespace ProductCatalogImport;

public sealed class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>Path to CSV file, relative to this tool's project directory unless absolute.</summary>
    public string CsvPath { get; set; } = "";

    /// <summary>Optional folder containing ProductsApi appsettings (to reuse DefaultConnection).</summary>
    public string ProductsApiSettingsDirectory { get; set; } = "";

    /// <summary>How many data rows to read after the header (batch size).</summary>
    public int MaxRows { get; set; } = 500;

    /// <summary>How many data rows to skip after the header (for the next batch, e.g. 500 then 1000).</summary>
    public int SkipRows { get; set; }

    public string RootCategoryName { get; set; } = "Electronics";

    /// <summary>How many INR equal one USD (e.g. 83 means 1 USD = 83 INR).</summary>
    public decimal InrPerUsd { get; set; } = 83m;

    public string PriceStoreName { get; set; } = "Import";

    public string DefaultSubCategoryName { get; set; } = "Other Electronics";
}
