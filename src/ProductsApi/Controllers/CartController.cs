using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Common;
using ProductsApi.Features.Cart;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Policy = "Cart.User")]
public sealed class CartController(CartService service) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Respond(await service.GetAsync(UserId, ct));

    [HttpPut("items/{productId:long}")]
    public async Task<IActionResult> Set(long productId, SetCartItemRequest request, CancellationToken ct) =>
        Respond(await service.SetAsync(UserId, productId, request, ct));

    [HttpDelete("items/{productId:long}")]
    public async Task<IActionResult> Remove(long productId, [FromQuery] Guid? version, CancellationToken ct) =>
        Respond(await service.RemoveAsync(UserId, productId, version, ct));

    [HttpDelete]
    public async Task<IActionResult> Clear([FromQuery] Guid? version, CancellationToken ct) =>
        Respond(await service.ClearAsync(UserId, version, ct));

    private IActionResult Respond(CartResult result) => result.Error is null
        ? Ok(result.Value) : StatusCode(result.StatusCode, new ErrorResponse(result.Error));
}
