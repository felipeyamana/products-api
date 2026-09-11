using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductsApi.Common.Cqrs;
using ProductsApi.Features.Categories.GetCategories;
using ProductsApi.Security;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.Products)]
public sealed class CategoriesController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ProductsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await queryDispatcher.Dispatch<GetCategoriesQuery, IReadOnlyList<CategoryDto>>(
            new GetCategoriesQuery(),
            cancellationToken);

        return Ok(categories);
    }
}
