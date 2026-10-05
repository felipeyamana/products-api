using Microsoft.EntityFrameworkCore;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Products.Shared;

namespace ProductsApi.Features.Favorites.Shared;

public sealed record CustomerFavoriteDto(
    DateTime CreatedAtUtc,
    ProductDto Product);

public sealed record AddCustomerFavoriteResult(
    CustomerFavoriteDto Favorite,
    bool WasCreated);

public enum FavoriteFailureKind
{
    None,
    BadRequest,
    NotFound,
    Conflict
}

public sealed record FavoriteResult<T>(
    T? Value,
    FavoriteFailureKind Failure,
    string? Error)
{
    public bool IsSuccess => Failure == FavoriteFailureKind.None;

    public static FavoriteResult<T> Success(T value) =>
        new(value, FavoriteFailureKind.None, null);

    public static FavoriteResult<T> BadRequest(string error) =>
        new(default, FavoriteFailureKind.BadRequest, error);

    public static FavoriteResult<T> NotFound(string error) =>
        new(default, FavoriteFailureKind.NotFound, error);

    public static FavoriteResult<T> Conflict(string error) =>
        new(default, FavoriteFailureKind.Conflict, error);
}

internal static class FavoriteMapper
{
    public static IQueryable<CustomerFavoriteDto> ProjectToDto(
        IQueryable<CustomerFavorite> favorites) =>
        favorites.Select(favorite => new CustomerFavoriteDto(
            favorite.CreatedAtUtc,
            new ProductDto(
                favorite.Product.Id,
                favorite.Product.Name,
                favorite.Product.Brand,
                favorite.Product.Description,
                favorite.Product.CategoryId,
                favorite.Product.Category.Name,
                favorite.Product.SubCategoryId,
                favorite.Product.SubCategory != null
                    ? favorite.Product.SubCategory.Name
                    : null,
                favorite.Product.ExternalProductId,
                favorite.Product.AverageRating,
                favorite.Product.TotalRatings,
                favorite.Product.IsActive,
                favorite.Product.CreatedAt,
                favorite.Product.UpdatedAt,
                favorite.Product.Prices
                    .OrderByDescending(price => price.CapturedAt)
                    .ThenByDescending(price => price.Id)
                    .Select(price => (decimal?)price.ActualPrice)
                    .FirstOrDefault(),
                favorite.Product.Prices
                    .OrderByDescending(price => price.CapturedAt)
                    .ThenByDescending(price => price.Id)
                    .Select(price => price.DiscountPrice)
                    .FirstOrDefault(),
                favorite.Product.Prices
                    .OrderByDescending(price => price.CapturedAt)
                    .ThenByDescending(price => price.Id)
                    .Select(price => string.IsNullOrEmpty(price.CurrencyCode.Trim())
                        ? null
                        : price.CurrencyCode.Trim())
                    .FirstOrDefault(),
                favorite.Product.Inventory == null
                    ? 0
                    : favorite.Product.Inventory.OnHand -
                      favorite.Product.Inventory.Reserved,
                favorite.Product.Inventory != null &&
                favorite.Product.Inventory.OnHand -
                favorite.Product.Inventory.Reserved > 0)));

    public static Task<CustomerFavoriteDto?> FindAsync(
        AppDbContext dbContext,
        long customerId,
        long productId,
        CancellationToken cancellationToken) =>
        ProjectToDto(dbContext.CustomerFavorites
            .AsNoTracking()
            .Where(favorite =>
                favorite.CustomerId == customerId &&
                favorite.ProductId == productId))
            .SingleOrDefaultAsync(cancellationToken);
}
