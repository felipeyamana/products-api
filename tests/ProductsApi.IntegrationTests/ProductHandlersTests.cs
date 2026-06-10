using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Products.CreateProduct;
using ProductsApi.Features.Products.DeleteProduct;
using ProductsApi.Features.Products.GetPagedProducts;
using ProductsApi.Features.Products.GetProductById;
using ProductsApi.Features.Products.Shared;
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

        await using var dbContext = fixture.CreateDbContext();
        await dbContext.RawProductImports.ExecuteDeleteAsync();
        await dbContext.ProductAttributes.ExecuteDeleteAsync();
        await dbContext.ProductImages.ExecuteDeleteAsync();
        await dbContext.ProductPrices.ExecuteDeleteAsync();
        await dbContext.Products.ExecuteDeleteAsync();
        await dbContext.Categories.ExecuteDeleteAsync();
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

    private static async Task<Category> CreateCategoryAsync(AppDbContext dbContext, string name)
    {
        var category = new Category { Name = name };
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
}
