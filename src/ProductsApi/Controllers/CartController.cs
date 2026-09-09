using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Common;
using ProductsApi.Features.Cart;
using ProductsApi.Security;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/cart")]
public sealed class CartController(CartService service) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CartRead)]
    public async Task<IActionResult> Get(CancellationToken ct) => Respond(await service.GetAsync(UserId, ct));

    [HttpPut("items/{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.CartWrite)]
    public async Task<IActionResult> Set(long productId, SetCartItemRequest request, CancellationToken ct) =>
        Respond(await service.SetAsync(UserId, productId, request, ct));

    [HttpDelete("items/{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.CartWrite)]
    public async Task<IActionResult> Remove(long productId, [FromQuery] Guid? version, CancellationToken ct) =>
        Respond(await service.RemoveAsync(UserId, productId, version, ct));

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.CartWrite)]
    public async Task<IActionResult> Clear([FromQuery] Guid? version, CancellationToken ct) =>
        Respond(await service.ClearAsync(UserId, version, ct));

    private IActionResult Respond(CartResult result) => result.Error is null
        ? Ok(result.Value) : StatusCode(result.StatusCode, new ErrorResponse(result.Error));
}
