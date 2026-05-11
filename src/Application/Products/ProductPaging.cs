namespace Application.Products;

public static class ProductPaging
{
    public const int MaxPageSize = 30;

    public const int DefaultPageSize = 30;

    /// <summary>
    /// Returns an error string if the page number is invalid, otherwise normalizes page size and returns null.
    /// </summary>
    public static string? NormalizePage(ref int pageNumber, ref int pageSize)
    {
        if (pageNumber < 1)
            return "Page must be at least 1.";

        if (pageSize < 1)
            pageSize = DefaultPageSize;

        if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;

        return null;
    }

    public static int TotalPages(int totalCount, int pageSize)
        => pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}
