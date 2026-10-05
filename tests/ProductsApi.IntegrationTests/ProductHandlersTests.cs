using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Products.CreateProduct;
using ProductsApi.Features.Products.DeleteProduct;
using ProductsApi.Features.Products.GetPagedProducts;
using ProductsApi.Features.Products.GetProductById;
using ProductsApi.Features.Products.RandomizeProductStock;
using ProductsApi.Features.Products.Shared;
using ProductsApi.Features.Categories.GetCategories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class ProductHandlersTests(MsSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateAndGetProduct_PersistsAndReadsCatalogDetails()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var category = await CreateCategoryAsync(dbContext, "Electronics");
        var createHandler = new CreateProductHandler(dbContext);

        var createResult = await createHandler.Handle(
            new CreateProductCommand(new CreateProductRequest
            {
                Name = " Mechanical Keyboard ",
                Brand = "KeyCo",
                CategoryId = category.Id,
                ExternalProductId = "keyboard-001",
                Price = 129.99m,
                ListPrice = 149.99m,
                PriceStoreName = "Main Store"
            }),
            CancellationToken.None);

        Assert.True(createResult.IsSuccess, createResult.Error);
        Assert.Equal("Mechanical Keyboard", createResult.Value!.Name);
        Assert.Equal("Electronics", createResult.Value.CategoryName);
        Assert.Equal(129.99m, createResult.Value.CurrentPrice);

        var getHandler = new GetProductByIdHandler(dbContext);
        var getResult = await getHandler.Handle(
            new GetProductByIdQuery(createResult.Value.Id),
            CancellationToken.None);

        Assert.True(getResult.IsSuccess, getResult.Error);
        Assert.Equal(createResult.Value.Id, getResult.Value!.Id);
        Assert.Equal("keyboard-001", getResult.Value.ExternalProductId);
    }

    [Fact]
    public async Task GetPagedProducts_OrdersAndPaginatesProductsFromSqlServer()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var category = await CreateCategoryAsync(dbContext, "Catalog");
        await CreateProductAsync(dbContext, category.Id, "Gamma");
        await CreateProductAsync(dbContext, category.Id, "Alpha");
        await CreateProductAsync(dbContext, category.Id, "Beta");
        var handler = new GetPagedProductsHandler(dbContext);

        var result = await handler.Handle(new GetPagedProductsQuery(1, 2), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Equal(["Alpha", "Beta"], result.Value.Items.Select(x => x.Name));
    }

    [Fact]
    public async Task GetPagedProducts_FiltersAndSortsByCatalogContract()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var electronics = await CreateCategoryAsync(dbContext, "Electronics");
        var audio = await CreateCategoryAsync(dbContext, "Audio", electronics.Id);
        var books = await CreateCategoryAsync(dbContext, "Books");
        var now = DateTime.UtcNow;
        var products = new[]
        {
            CreateCatalogProduct("Budget", "Acme", electronics.Id, audio.Id, 4.7m, now.AddDays(-4), (50m, now)),
            CreateCatalogProduct(
                "Mid-range",
                "Acme",
                electronics.Id,
                audio.Id,
                4.6m,
                now.AddDays(-3),
                (300m, now.AddDays(-2)),
                (100m, now)),
            CreateCatalogProduct("Lower rated", "Contoso", electronics.Id, audio.Id, 3.5m, now.AddDays(-2), (120m, now)),
            CreateCatalogProduct("Premium", "Contoso", electronics.Id, audio.Id, 4.9m, now.AddDays(-2), (150m, now)),
            CreateCatalogProduct("Unpriced", "Acme", electronics.Id, audio.Id, 5m, now.AddDays(-1)),
            CreateCatalogProduct("Other category", "Acme", books.Id, null, 4.9m, now, (125m, now))
        };
        await dbContext.Products.AddRangeAsync(products);
        await dbContext.SaveChangesAsync();
        var handler = new GetPagedProductsHandler(dbContext);

        var filteredResult = await handler.Handle(
            new GetPagedProductsQuery(
                PageNumber: 1,
                PageSize: 30,
                CategoryId: electronics.Id,
                SubCategoryId: audio.Id,
                Brands: [" Acme, Contoso "],
                MinPrice: 75m,
                MaxPrice: 160m,
                MinRating: 4.5m,
                Sort: ProductCatalogSort.PriceDescending),
            CancellationToken.None);

        Assert.True(filteredResult.IsSuccess, filteredResult.Error);
        Assert.Equal(["Premium", "Mid-range"], filteredResult.Value!.Items.Select(product => product.Name));
        Assert.Equal([150m, 100m], filteredResult.Value.Items.Select(product => product.CurrentPrice));
        Assert.Equal(50m, filteredResult.Value.Facets.MinPrice);
        Assert.Equal(150m, filteredResult.Value.Facets.MaxPrice);
        Assert.Equal(
            [new ProductBrandFacetDto("Acme", 1), new ProductBrandFacetDto("Contoso", 1)],
            filteredResult.Value.Facets.Brands);
        Assert.Equal(
            [
                new ProductRatingFacetDto(4m, 2),
                new ProductRatingFacetDto(3m, 3),
                new ProductRatingFacetDto(2m, 3),
                new ProductRatingFacetDto(1m, 3)
            ],
            filteredResult.Value.Facets.Ratings);

        var priceAscendingResult = await handler.Handle(
            new GetPagedProductsQuery(
                1,
                30,
                CategoryId: electronics.Id,
                Sort: ProductCatalogSort.PriceAscending),
            CancellationToken.None);

        Assert.True(priceAscendingResult.IsSuccess, priceAscendingResult.Error);
        Assert.Equal(
            ["Budget", "Mid-range", "Lower rated", "Premium", "Unpriced"],
            priceAscendingResult.Value!.Items.Select(product => product.Name));
    }

    [Fact]
    public async Task GetPagedProducts_RejectsSearchOverMaximumLength()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var handler = new GetPagedProductsHandler(dbContext);

        var result = await handler.Handle(
            new GetPagedProductsQuery(1, 30, new string('a', ProductConstraints.MaxSearchLength + 1)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(ProductConstraints.MaxSearchLength.ToString(), result.Error);
    }

    [Fact]
    public async Task GetCategories_OrdersCategoriesAndReturnsTheirHierarchyReferences()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var parent = await CreateCategoryAsync(dbContext, "Electronics");
        await CreateCategoryAsync(dbContext, "Accessories", parent.Id);
        await CreateCategoryAsync(dbContext, "Books");
        var handler = new GetCategoriesHandler(dbContext);

        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Equal(["Accessories", "Books", "Electronics"], result.Select(x => x.Name));
        Assert.Equal(parent.Id, result.Single(x => x.Name == "Accessories").ParentCategoryId);
    }

    [Fact]
    public async Task CreateProduct_RejectsDuplicateExternalProductId()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var category = await CreateCategoryAsync(dbContext, "Catalog");
        await CreateProductAsync(dbContext, category.Id, "Existing", "duplicate-id");
        var handler = new CreateProductHandler(dbContext);

        var result = await handler.Handle(
            new CreateProductCommand(new CreateProductRequest
            {
                Name = "Duplicate",
                CategoryId = category.Id,
                ExternalProductId = "duplicate-id"
            }),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task DeleteProduct_RemovesProductFromDatabase()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var category = await CreateCategoryAsync(dbContext, "Catalog");
        var product = await CreateProductAsync(dbContext, category.Id, "Disposable");
        var handler = new DeleteProductHandler(dbContext);

        var result = await handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(await dbContext.Products.AnyAsync(x => x.Id == product.Id));
    }

    [Fact]
    public async Task RandomizeProductStock_UsesStableBatchesAndCreatesInventory()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await using var dbContext = fixture.CreateDbContext();
        var category = await CreateCategoryAsync(dbContext, "Inventory");
        var products = new[]
        {
            await CreateProductAsync(dbContext, category.Id, "First"),
            await CreateProductAsync(dbContext, category.Id, "Second"),
            await CreateProductAsync(dbContext, category.Id, "Third")
        };
        var handler = new RandomizeProductStockHandler(dbContext);

        var firstBatch = await handler.Handle(
            new RandomizeProductStockCommand(
                new RandomizeProductStockRequest(
                    BatchSize: 2,
                    MinimumAvailableStock: 12,
                    MaximumAvailableStock: 12)),
            CancellationToken.None);

        Assert.True(firstBatch.IsSuccess, firstBatch.Error);
        Assert.Equal(2, firstBatch.Value!.AssignedCount);
        Assert.True(firstBatch.Value.HasMore);
        Assert.Equal(products[1].Id, firstBatch.Value.NextAfterProductId);
        Assert.All(firstBatch.Value.Items, item => Assert.Equal(12, item.Available));

        var secondBatch = await handler.Handle(
            new RandomizeProductStockCommand(
                new RandomizeProductStockRequest(
                    BatchSize: 2,
                    AfterProductId: firstBatch.Value.NextAfterProductId,
                    MinimumAvailableStock: 7,
                    MaximumAvailableStock: 7)),
            CancellationToken.None);

        Assert.Single(secondBatch.Value!.Items);
        Assert.False(secondBatch.Value.HasMore);
        Assert.Equal(products[2].Id, secondBatch.Value.Items[0].ProductId);
        Assert.Equal(7, secondBatch.Value.Items[0].Available);
        Assert.Equal(3, await dbContext.ProductInventories.CountAsync());
    }

    private static async Task<Category> CreateCategoryAsync(
        AppDbContext dbContext,
        string name,
        int? parentCategoryId = null)
    {
        var category = new Category { Name = name, ParentCategoryId = parentCategoryId };
        await dbContext.Categories.AddAsync(category);
        await dbContext.SaveChangesAsync();
        return category;
    }

    private static async Task<Product> CreateProductAsync(
        AppDbContext dbContext,
        int categoryId,
        string name,
        string? externalProductId = null)
    {
        var product = new Product
        {
            Name = name,
            Brand = "Brand",
            CategoryId = categoryId,
            ExternalProductId = externalProductId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.Products.AddAsync(product);
        await dbContext.SaveChangesAsync();
        return product;
    }

    private static Product CreateCatalogProduct(
        string name,
        string brand,
        int categoryId,
        int? subCategoryId,
        decimal averageRating,
        DateTime createdAt,
        params (decimal Price, DateTime CapturedAt)[] prices) =>
        new()
        {
            Name = name,
            Brand = brand,
            CategoryId = categoryId,
            SubCategoryId = subCategoryId,
            AverageRating = averageRating,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Prices = prices
                .Select(price => new ProductPrice
                {
                    ActualPrice = price.Price,
                    CurrencyCode = "USD",
                    CapturedAt = price.CapturedAt
                })
                .ToList()
        };
}
