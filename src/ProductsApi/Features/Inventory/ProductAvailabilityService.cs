using Microsoft.EntityFrameworkCore;
using ProductsApi.Data;
using ProductsApi.Features.Products.Shared;

namespace ProductsApi.Features.Inventory;

public sealed class ProductAvailabilityService(
    AppDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<ProductDto> RefreshAsync(
        ProductDto product,
        CancellationToken cancellationToken)
    {
        var availability = await GetAvailabilityAsync(
            [product.Id],
            cancellationToken);
        var available = availability.GetValueOrDefault(product.Id);
        return product with
        {
            AvailableStock = available,
            IsInStock = available > 0
        };
    }

    public async Task<PagedProductsDto> RefreshAsync(
        PagedProductsDto products,
        CancellationToken cancellationToken)
    {
        var availability = await GetAvailabilityAsync(
            products.Items.Select(product => product.Id),
            cancellationToken);
        var items = products.Items
            .Select(product =>
            {
                var available = availability.GetValueOrDefault(product.Id);
                return product with
                {
                    AvailableStock = available,
                    IsInStock = available > 0
                };
            })
            .ToArray();

        return products with { Items = items };
    }

    private Task<Dictionary<long, int>> GetAvailabilityAsync(
        IEnumerable<long> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        return dbContext.Products
            .AsNoTracking()
            .Where(product => ids.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                Available = product.Inventory == null
                    ? 0
                    : product.Inventory.OnHand -
                      (product.InventoryReservations
                          .Where(reservation =>
                              reservation.ExpiresAtUtc > utcNow)
                          .Sum(reservation =>
                              (int?)reservation.Quantity) ?? 0)
            })
            .ToDictionaryAsync(
                product => product.Id,
                product => product.Available,
                cancellationToken);
    }
}
