using System.Data;
using Microsoft.Data.SqlClient;
using ProductCatalogImport.Csv;
using ProductCatalogImport.Pricing;
using ProductCatalogImport.SubCategory;

namespace ProductCatalogImport.Sql;

public sealed class CatalogImporter
{
    private readonly string _connectionString;
    private readonly ImportOptions _options;
    private readonly TitleSubCategoryResolver _subCategories;

    public CatalogImporter(string connectionString, ImportOptions options)
    {
        _connectionString = connectionString;
        _options = options;
        _subCategories = new TitleSubCategoryResolver(options.DefaultSubCategoryName);
    }

    public async Task ImportAsync(IEnumerable<ElectronicsRow> rows, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(cancellationToken);

        var rootCategoryId = await EnsureRootCategoryAsync(conn, tran, cancellationToken);
        var subCategoryCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var processed = 0;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryPrepareRow(row, out var prepared))
            {
                Console.WriteLine($"Skip (invalid): {row.Name[..Math.Min(60, row.Name.Length)]}...");
                continue;
            }

            var subName = _subCategories.Resolve(prepared.Title);
            if (!subCategoryCache.TryGetValue(subName, out var subCategoryId))
            {
                subCategoryId = await EnsureChildCategoryAsync(conn, tran, subName, rootCategoryId, cancellationToken);
                subCategoryCache[subName] = subCategoryId;
            }

            var productId = await UpsertProductAsync(conn, tran, prepared, rootCategoryId, subCategoryId, cancellationToken);
            await ReplaceLatestPriceAsync(conn, tran, productId, prepared, cancellationToken);

            processed++;
            if (processed % 50 == 0)
                Console.WriteLine($"Imported {processed} products...");
        }

        await tran.CommitAsync(cancellationToken);
        Console.WriteLine($"Done. Committed {processed} products.");
    }

    private async Task<int> EnsureRootCategoryAsync(
        SqlConnection conn,
        SqlTransaction tran,
        CancellationToken ct)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM [Categories] WHERE [Name] = @Name AND [ParentCategoryId] IS NULL)
                INSERT INTO [Categories] ([Name], [ParentCategoryId]) VALUES (@Name, NULL);

            SELECT TOP (1) [Id] FROM [Categories] WHERE [Name] = @Name AND [ParentCategoryId] IS NULL;
            """;

        await using var cmd = new SqlCommand(sql, conn, tran);
        cmd.Parameters.AddWithValue("@Name", _options.RootCategoryName);
        var id = (int)(await cmd.ExecuteScalarAsync(ct))!;
        return id;
    }

    private static async Task<int> EnsureChildCategoryAsync(
        SqlConnection conn,
        SqlTransaction tran,
        string name,
        int parentId,
        CancellationToken ct)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM [Categories] WHERE [Name] = @Name AND [ParentCategoryId] = @ParentId)
                INSERT INTO [Categories] ([Name], [ParentCategoryId]) VALUES (@Name, @ParentId);

            SELECT TOP (1) [Id] FROM [Categories] WHERE [Name] = @Name AND [ParentCategoryId] = @ParentId;
            """;

        await using var cmd = new SqlCommand(sql, conn, tran);
        cmd.Parameters.AddWithValue("@Name", Truncate(name, 150));
        cmd.Parameters.AddWithValue("@ParentId", parentId);
        var id = (int)(await cmd.ExecuteScalarAsync(ct))!;
        return id;
    }

    private async Task<long> UpsertProductAsync(
        SqlConnection conn,
        SqlTransaction tran,
        PreparedRow p,
        int categoryId,
        int subCategoryId,
        CancellationToken ct)
    {
        const string mergeSql = """
            DECLARE @Id BIGINT;

            SELECT @Id = [Id] FROM [Products] WHERE [ExternalProductId] = @ExternalProductId;

            IF @Id IS NULL
            BEGIN
                INSERT INTO [Products] (
                    [Name], [Brand], [Description], [CategoryId], [SubCategoryId],
                    [SourceUrl], [ExternalProductId], [AverageRating], [TotalRatings], [IsActive])
                VALUES (
                    @Name, @Brand, @Description, @CategoryId, @SubCategoryId,
                    @SourceUrl, @ExternalProductId, @AverageRating, @TotalRatings, 1);

                SET @Id = CAST(SCOPE_IDENTITY() AS BIGINT);
            END
            ELSE
            BEGIN
                UPDATE [Products]
                SET
                    [Name] = @Name,
                    [CategoryId] = @CategoryId,
                    [SubCategoryId] = @SubCategoryId,
                    [SourceUrl] = @SourceUrl,
                    [AverageRating] = @AverageRating,
                    [TotalRatings] = @TotalRatings,
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Id] = @Id;
            END

            SELECT @Id;
            """;

        await using var cmd = new SqlCommand(mergeSql, conn, tran);
        cmd.Parameters.AddWithValue("@Name", Truncate(p.Title, 500));
        cmd.Parameters.AddWithValue("@Brand", (object?)Truncate(p.Brand, 150) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", DBNull.Value);
        cmd.Parameters.AddWithValue("@CategoryId", categoryId);
        cmd.Parameters.AddWithValue("@SubCategoryId", subCategoryId);
        cmd.Parameters.AddWithValue("@SourceUrl", (object?)Truncate(p.SourceUrl, 1000) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ExternalProductId", p.ExternalProductId);
        cmd.Parameters.AddWithValue("@AverageRating", (object?)p.AverageRating ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TotalRatings", (object?)p.TotalRatings ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (long)result!;
    }

    private async Task ReplaceLatestPriceAsync(
        SqlConnection conn,
        SqlTransaction tran,
        long productId,
        PreparedRow p,
        CancellationToken ct)
    {
        const string del = "DELETE FROM [ProductPrices] WHERE [ProductId] = @ProductId;";
        await using (var d = new SqlCommand(del, conn, tran))
        {
            d.Parameters.AddWithValue("@ProductId", productId);
            await d.ExecuteNonQueryAsync(ct);
        }

        const string ins = """
            INSERT INTO [ProductPrices] (
                [ProductId], [StoreName], [ActualPrice], [DiscountPrice], [CurrencyCode], [ProductUrl], [CapturedAt])
            VALUES (
                @ProductId, @StoreName, @ActualPrice, @DiscountPrice, 'USD', @ProductUrl, SYSUTCDATETIME());
            """;

        await using var cmd = new SqlCommand(ins, conn, tran);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@StoreName", Truncate(_options.PriceStoreName, 150));
        cmd.Parameters.AddWithValue("@ActualPrice", p.PayPriceUsd);
        cmd.Parameters.AddWithValue(
            "@DiscountPrice",
            p.ListOrMrpUsd.HasValue ? p.ListOrMrpUsd.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ProductUrl", (object?)Truncate(p.SourceUrl, 1000) ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private bool TryPrepareRow(ElectronicsRow row, out PreparedRow prepared)
    {
        prepared = default!;

        var title = row.Name?.Trim() ?? "";
        if (title.Length == 0)
            return false;

        var ext = ExtractAsin(row.Link);
        if (ext is null)
            return false;

        // CSV: discount_price = current/selling, actual_price = typical MRP/list (when present).
        if (!InrMoneyParser.TryParseInr(row.ActualPrice, out var inrMrp))
            return false;

        var hasSelling = InrMoneyParser.TryParseInr(row.DiscountPrice, out var inrSelling)
            && inrSelling > 0
            && inrSelling <= inrMrp;

        var payInr = hasSelling ? inrSelling : inrMrp;
        var payUsd = InrMoneyParser.ToUsd(payInr, _options.InrPerUsd);
        if (payUsd is null)
            return false;

        decimal? mrpUsd = null;
        if (hasSelling && inrMrp > inrSelling)
            mrpUsd = InrMoneyParser.ToUsd(inrMrp, _options.InrPerUsd);

        decimal? avg = null;
        if (decimal.TryParse(row.Ratings?.Trim(), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var r))
            avg = Math.Round(r, 2, MidpointRounding.AwayFromZero);

        int? total = null;
        var ratingsRaw = row.NoOfRatings?.Replace(",", "", StringComparison.Ordinal).Trim();
        if (int.TryParse(ratingsRaw, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var tr))
            total = tr;

        var brand = GuessBrand(title);

        prepared = new PreparedRow(
            Title: title,
            Brand: brand,
            SourceUrl: row.Link?.Trim(),
            ExternalProductId: ext,
            AverageRating: avg,
            TotalRatings: total,
            PayPriceUsd: payUsd.Value,
            ListOrMrpUsd: mrpUsd);

        return true;
    }

    private static string? GuessBrand(string title)
    {
        ReadOnlySpan<string> prefixes =
        [
            "Apple", "Samsung", "OnePlus", "Redmi", "Xiaomi", "realme", "Google", "Sony", "boAt", "JBL",
            "Fire-Boltt", "Noise", "HP", "Dell", "Lenovo", "Asus", "MSI"
        ];

        foreach (var p in prefixes)
        {
            if (title.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return p;
        }

        return null;
    }

    private static string? ExtractAsin(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return null;

        var m = System.Text.RegularExpressions.Regex.Match(link, @"/dp/([A-Z0-9]{10})(?:[/?]|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.Length <= max ? value : value[..max];
    }

    private sealed record PreparedRow(
        string Title,
        string? Brand,
        string? SourceUrl,
        string ExternalProductId,
        decimal? AverageRating,
        int? TotalRatings,
        decimal PayPriceUsd,
        decimal? ListOrMrpUsd);
}
