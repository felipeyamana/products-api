using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Features.Favorites.AddCustomerFavorite;
using ProductsApi.Features.Favorites.GetCustomerFavorites;
using ProductsApi.Features.Favorites.RemoveCustomerFavorite;
using ProductsApi.Features.Favorites.Shared;
using ProductsApi.Security;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/customers/me/favorites")]
public sealed class CustomerFavoritesController(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.FavoritesRead)]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerFavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFavorites(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Dispatch<
            GetCustomerFavoritesQuery,
            FavoriteResult<IReadOnlyList<CustomerFavoriteDto>>>(
                new GetCustomerFavoritesQuery(UserId),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.FavoritesWrite)]
    [ProducesResponseType(typeof(CustomerFavoriteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CustomerFavoriteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddFavorite(
        long productId,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            AddCustomerFavoriteCommand,
            FavoriteResult<AddCustomerFavoriteResult>>(
                new AddCustomerFavoriteCommand(UserId, productId),
                cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var outcome = result.Value!;
        return outcome.WasCreated
            ? CreatedAtAction(nameof(GetFavorites), outcome.Favorite)
            : Ok(outcome.Favorite);
    }

    [HttpDelete("{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.FavoritesWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(
        long productId,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            RemoveCustomerFavoriteCommand,
            FavoriteResult<bool>>(
                new RemoveCustomerFavoriteCommand(UserId, productId),
                cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(FavoriteResult<T> result) =>
        result.Failure switch
        {
            FavoriteFailureKind.None => Ok(result.Value),
            FavoriteFailureKind.BadRequest => BadRequest(new ErrorResponse(result.Error!)),
            FavoriteFailureKind.NotFound => NotFound(new ErrorResponse(result.Error!)),
            FavoriteFailureKind.Conflict => Conflict(new ErrorResponse(result.Error!)),
            _ => throw new InvalidOperationException("Unknown favorite result.")
        };
}
