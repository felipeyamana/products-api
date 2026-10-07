using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Favorites.Shared;

namespace ProductsApi.Features.Favorites.GetCustomerFavorites;

public sealed record GetCustomerFavoritesQuery(Guid UserId);

public sealed class GetCustomerFavoritesHandler(AppDbContext dbContext)
    : IQueryHandler<
        GetCustomerFavoritesQuery,
        FavoriteResult<IReadOnlyList<CustomerFavoriteDto>>>
{
    public async Task<FavoriteResult<IReadOnlyList<CustomerFavoriteDto>>> Handle(
        GetCustomerFavoritesQuery query,
        CancellationToken cancellationToken)
    {
        var customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.UserId == query.UserId, cancellationToken);
        if (!customerExists)
        {
            return FavoriteResult<IReadOnlyList<CustomerFavoriteDto>>
                .NotFound("Customer record not found.");
        }

        var favorites = await FavoriteMapper.ProjectToDto(
                dbContext.CustomerFavorites
                    .AsNoTracking()
                    .Where(favorite => favorite.Customer.UserId == query.UserId)
                    .OrderByDescending(favorite => favorite.CreatedAtUtc)
                    .ThenBy(favorite => favorite.Product.Name)
                    .ThenBy(favorite => favorite.ProductId),
                DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        return FavoriteResult<IReadOnlyList<CustomerFavoriteDto>>.Success(favorites);
    }
}
