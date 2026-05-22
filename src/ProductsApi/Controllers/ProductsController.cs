using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Features.Products.CreateProduct;
using ProductsApi.Features.Products.DeleteProduct;
using ProductsApi.Features.Products.GetPagedProducts;
using ProductsApi.Features.Products.GetProductById;
using ProductsApi.Features.Products.PatchProduct;
using ProductsApi.Features.Products.ReplaceProduct;
using ProductsApi.Features.Products.Shared;
using Microsoft.AspNetCore.Mvc;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(IQueryDispatcher queryDispatcher, ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedProductsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ProductPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.Dispatch<GetPagedProductsQuery, Result<PagedProductsDto>>(
            new GetPagedProductsQuery(page, pageSize),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(long id, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Dispatch<GetProductByIdQuery, Result<ProductDto>>(
            new GetProductByIdQuery(id),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.Error });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
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
            return CreatedAtAction(
                nameof(GetProduct),
                new { id = result.Value!.Id },
                result.Value);
        }

        return ToErrorResponse(result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
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

        return result.IsSuccess
            ? Ok(result.Value)
            : ToErrorResponse(result);
    }

    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
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

        return result.IsSuccess
            ? Ok(result.Value)
            : ToErrorResponse(result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(long id, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<DeleteProductCommand, Result<bool>>(
            new DeleteProductCommand(id),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(new { message = result.Error });
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
