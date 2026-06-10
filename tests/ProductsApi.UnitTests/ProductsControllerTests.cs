using ProductsApi.Caching;
using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Controllers;
using ProductsApi.Features.Products.GetPagedProducts;
using ProductsApi.Features.Products.GetProductById;
using ProductsApi.Features.Products.ReplaceProduct;
using ProductsApi.Features.Products.Shared;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ProductsApi.UnitTests;

public class ProductsControllerTests
{
    [Fact]
    public async Task GetProduct_WhenCached_ReturnsCachedProductWithoutDispatchingQuery()
    {
        var product = CreateProduct(10);
        var cache = new TestProductCache { Product = product };
        var queryDispatcher = new Mock<IQueryDispatcher>();
        var controller = CreateController(queryDispatcher: queryDispatcher, cache: cache);

        var response = await controller.GetProduct(product.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Same(product, okResult.Value);
        queryDispatcher.Verify(
            x => x.Dispatch<GetProductByIdQuery, Result<ProductDto>>(
                It.IsAny<GetProductByIdQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceProduct_WhenCommandSucceeds_InvalidatesAndRefreshesCache()
    {
        var product = CreateProduct(5);
        var cache = new TestProductCache();
        var commandDispatcher = new Mock<ICommandDispatcher>();
        commandDispatcher
            .Setup(x => x.Dispatch<ReplaceProductCommand, Result<ProductDto>>(
                It.IsAny<ReplaceProductCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDto>.Ok(product));
        var controller = CreateController(commandDispatcher: commandDispatcher, cache: cache);

        var response = await controller.ReplaceProduct(
            product.Id,
            new UpdateProductRequest { Name = product.Name, CategoryId = product.CategoryId },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Same(product, okResult.Value);
        Assert.Equal(product.Id, cache.InvalidatedProductId);
        Assert.True(cache.InvalidatedProducts);
        Assert.Same(product, cache.Product);
    }

    private static ProductsController CreateController(
        Mock<IQueryDispatcher>? queryDispatcher = null,
        Mock<ICommandDispatcher>? commandDispatcher = null,
        IProductCache? cache = null)
    {
        return new ProductsController(
            (queryDispatcher ?? new Mock<IQueryDispatcher>()).Object,
            (commandDispatcher ?? new Mock<ICommandDispatcher>()).Object,
            cache is null ? [] : [cache]);
    }

    private static ProductDto CreateProduct(long id) =>
        new(
            id,
            $"Product {id}",
            "Brand",
            "Description",
            1,
            "Category",
            null,
            null,
            $"external-{id}",
            4.5m,
            10,
            true,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            9.99m,
            14.99m,
            "USD");

    private sealed class TestProductCache : IProductCache
    {
        public PagedProductsDto? PagedProducts { get; private set; }

        public ProductDto? Product { get; set; }

        public long? InvalidatedProductId { get; private set; }

        public bool InvalidatedProducts { get; private set; }

        public Task<PagedProductsDto?> GetPagedProductsAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(PagedProducts);

        public Task SetPagedProductsAsync(PagedProductsDto products, CancellationToken cancellationToken)
        {
            PagedProducts = products;
            return Task.CompletedTask;
        }

        public Task<ProductDto?> GetProductAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Product);

        public Task SetProductAsync(ProductDto product, CancellationToken cancellationToken)
        {
            Product = product;
            return Task.CompletedTask;
        }

        public Task InvalidateProductAsync(long id, CancellationToken cancellationToken)
        {
            InvalidatedProductId = id;
            return Task.CompletedTask;
        }

        public Task InvalidateProductsAsync(CancellationToken cancellationToken)
        {
            InvalidatedProducts = true;
            return Task.CompletedTask;
        }
    }
}
