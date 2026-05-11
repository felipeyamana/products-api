namespace Application.Products;

public static class ProductPaging
{
    public const int MaxPageSize = 30;

    public const int DefaultPageSize = 30;

    /// <summary>
    /// Ensures page is valid and page size is between 1 and <see cref="MaxPageSize"/>.
    /// </summary>
    public static void NormalizePage(ref int pageNumber, ref int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page must be at least 1.");

        if (pageSize < 1)
            pageSize = DefaultPageSize;

        if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;
    }

    public static int TotalPages(int totalCount, int pageSize)
        => pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}
