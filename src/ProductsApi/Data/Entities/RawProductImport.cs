namespace ProductsApi.Data.Entities;

public class RawProductImport
{
    public long Id { get; set; }

    public string SourceName { get; set; } = null!;

    public string? ExternalProductId { get; set; }

    public string RawJson { get; set; } = null!;

    public bool Processed { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime ImportedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
