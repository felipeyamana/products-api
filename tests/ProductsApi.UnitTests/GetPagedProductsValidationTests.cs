using Microsoft.EntityFrameworkCore;
using ProductsApi.Data;
using ProductsApi.Features.Products.GetPagedProducts;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class GetPagedProductsValidationTests
{
    [Theory]
    [InlineData(-1d, null, null, "MinPrice")]
    [InlineData(null, 6d, null, "MinRating")]
    [InlineData(null, null, "popular", "Sort")]
    public async Task Handle_RejectsInvalidCatalogOptions(
        double? minPrice,
        double? minRating,
        string? sort,
        string expectedError)
    {
        await using var dbContext = CreateDbContext();
        var handler = new GetPagedProductsHandler(dbContext);

        var result = await handler.Handle(
            new GetPagedProductsQuery(
                PageNumber: 1,
                PageSize: 30,
                MinPrice: minPrice is null ? null : Convert.ToDecimal(minPrice.Value),
                MinRating: minRating is null ? null : Convert.ToDecimal(minRating.Value),
                Sort: sort),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public async Task Handle_RejectsInvertedPriceRange()
    {
        await using var dbContext = CreateDbContext();
        var handler = new GetPagedProductsHandler(dbContext);

        var result = await handler.Handle(
            new GetPagedProductsQuery(1, 30, MinPrice: 20m, MaxPrice: 10m),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("MinPrice", result.Error);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ProductsApiValidationTests;Trusted_Connection=True")
            .Options;
        return new AppDbContext(options);
    }
}
