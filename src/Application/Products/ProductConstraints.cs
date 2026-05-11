namespace Application.Products;

/// <summary>Shared limits for product payloads (requests + persistence).</summary>
public static class ProductConstraints
{
    public const int MaxNameLength = 500;
    public const int MaxBrandLength = 150;
    public const int MaxUrlLength = 1000;
    public const int MaxExternalIdLength = 100;
    public const int MaxStoreNameLength = 150;
}
