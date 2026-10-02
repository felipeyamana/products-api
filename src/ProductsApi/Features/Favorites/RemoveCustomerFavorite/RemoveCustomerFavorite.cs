using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Favorites.Shared;

namespace ProductsApi.Features.Favorites.RemoveCustomerFavorite;

public sealed record RemoveCustomerFavoriteCommand(Guid UserId, long ProductId);

public sealed class RemoveCustomerFavoriteHandler(AppDbContext dbContext)
    : ICommandHandler<RemoveCustomerFavoriteCommand, FavoriteResult<bool>>
{
    public async Task<FavoriteResult<bool>> Handle(
        RemoveCustomerFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ProductId <= 0)
        {
            return FavoriteResult<bool>
                .BadRequest("Product ID must be positive.");
        }

        var customerId = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.UserId == command.UserId)
            .Select(customer => (long?)customer.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return FavoriteResult<bool>
                .NotFound("Customer record not found.");
        }

        await dbContext.CustomerFavorites
            .Where(favorite =>
                favorite.CustomerId == customerId.Value &&
                favorite.ProductId == command.ProductId)
            .ExecuteDeleteAsync(cancellationToken);

        return FavoriteResult<bool>.Success(true);
    }
}
