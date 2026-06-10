using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Caching;
using ProductsApi.Features.Products.CreateProduct;
using ProductsApi.Features.Products.DeleteProduct;
using ProductsApi.Features.Products.GetPagedProducts;
using ProductsApi.Features.Products.GetProductById;
using ProductsApi.Features.Products.PatchProduct;
using ProductsApi.Features.Products.ReplaceProduct;
using ProductsApi.Features.Products.Shared;
using ProductsApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.Products)]
public class ProductsController(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher,
    IEnumerable<IProductCache> productCaches) : ControllerBase
{
    private readonly IProductCache? _productCache = productCaches.FirstOrDefault();

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ProductsRead)]
    [ProducesResponseType(typeof(PagedProductsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ProductPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var cachedProducts = _productCache is null
            ? null
            : await _productCache.GetPagedProductsAsync(page, pageSize, cancellationToken);
        if (cachedProducts is not null)
        {
            return Ok(cachedProducts);
        }

        var result = await queryDispatcher.Dispatch<GetPagedProductsQuery, Result<PagedProductsDto>>(
            new GetPagedProductsQuery(page, pageSize),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Error });
        }

        if (_productCache is not null)
        {
            await _productCache.SetPagedProductsAsync(result.Value!, cancellationToken);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.ProductsRead)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(long id, CancellationToken cancellationToken)
    {
        var cachedProduct = _productCache is null
            ? null
            : await _productCache.GetProductAsync(id, cancellationToken);
        if (cachedProduct is not null)
        {
            return Ok(cachedProduct);
        }

        var result = await queryDispatcher.Dispatch<GetProductByIdQuery, Result<ProductDto>>(
            new GetProductByIdQuery(id),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new { message = result.Error });
        }

        if (_productCache is not null)
        {
            await _productCache.SetProductAsync(result.Value!, cancellationToken);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ProductsWrite)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<CreateProductCommand, Result<ProductDto>>(
            new CreateProductCommand(request),
            cancellationToken);

        if (result.IsSuccess)
        {
            if (_productCache is not null)
            {
                await _productCache.InvalidateProductsAsync(cancellationToken);
            }

            return CreatedAtAction(
                nameof(GetProduct),
                new { id = result.Value!.Id },
                result.Value);
        }

        return ToErrorResponse(result);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.ProductsWrite)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplaceProduct(
        long id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<ReplaceProductCommand, Result<ProductDto>>(
            new ReplaceProductCommand(id, request),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResponse(result);
        }

        if (_productCache is not null)
        {
            await _productCache.InvalidateProductAsync(id, cancellationToken);
            await _productCache.InvalidateProductsAsync(cancellationToken);
            await _productCache.SetProductAsync(result.Value!, cancellationToken);
        }

        return Ok(result.Value);
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.ProductsWrite)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PatchProduct(
        long id,
        [FromBody] PatchProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<PatchProductCommand, Result<ProductDto>>(
            new PatchProductCommand(id, request),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResponse(result);
        }

        if (_productCache is not null)
        {
            await _productCache.InvalidateProductAsync(id, cancellationToken);
            await _productCache.InvalidateProductsAsync(cancellationToken);
            await _productCache.SetProductAsync(result.Value!, cancellationToken);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.ProductsWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(long id, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<DeleteProductCommand, Result<bool>>(
            new DeleteProductCommand(id),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new { message = result.Error });
        }

        if (_productCache is not null)
        {
            await _productCache.InvalidateProductAsync(id, cancellationToken);
            await _productCache.InvalidateProductsAsync(cancellationToken);
        }

        return NoContent();
    }

    private IActionResult ToErrorResponse<T>(Result<T> result)
    {
        if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = result.Error });

        if (result.Error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = result.Error });

        return BadRequest(new { message = result.Error });
    }
}
