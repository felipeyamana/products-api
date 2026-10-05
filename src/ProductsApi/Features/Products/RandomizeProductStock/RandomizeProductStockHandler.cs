using Microsoft.EntityFrameworkCore;
using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;

namespace ProductsApi.Features.Products.RandomizeProductStock;

public sealed class RandomizeProductStockHandler(AppDbContext dbContext)
    : ICommandHandler<RandomizeProductStockCommand, Result<RandomizeProductStockDto>>
{
    public const int MaximumBatchSize = 500;
    public const int MaximumStockLimit = 1_000_000;

    public async Task<Result<RandomizeProductStockDto>> Handle(
        RandomizeProductStockCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Result<RandomizeProductStockDto>.Fail(validationError);
        }

        var afterProductId = request.AfterProductId ?? 0;
        var products = await dbContext.Products
            .Where(product => product.Id > afterProductId)
            .OrderBy(product => product.Id)
            .Take(request.BatchSize)
            .Include(product => product.Inventory)
            .Include(product => product.InventoryReservations)
            .ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        var randomized = new List<RandomizedProductStockDto>(products.Count);

        foreach (var product in products)
        {
            var inventory = product.Inventory;
            if (inventory is null)
            {
                inventory = new ProductInventory
                {
                    ProductId = product.Id,
                    Product = product,
                    UpdatedAtUtc = utcNow
                };
                product.Inventory = inventory;
                dbContext.ProductInventories.Add(inventory);
            }

            var available = GenerateAvailableStock(
                request.MinimumAvailableStock,
                request.MaximumAvailableStock);
            var reserved = product.InventoryReservations
                .Where(reservation => reservation.ExpiresAtUtc > utcNow)
                .Sum(reservation => reservation.Quantity);
            inventory.OnHand = checked(reserved + available);
            inventory.UpdatedAtUtc = utcNow;

            randomized.Add(new RandomizedProductStockDto(
                product.Id,
                inventory.OnHand,
                reserved,
                available));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        long? nextAfterProductId = products.Count == 0 ? null : products[^1].Id;
        var hasMore = nextAfterProductId is not null &&
            await dbContext.Products.AnyAsync(
                product => product.Id > nextAfterProductId.Value,
                cancellationToken);

        return Result<RandomizeProductStockDto>.Ok(
            new RandomizeProductStockDto(
                randomized,
                randomized.Count,
                nextAfterProductId,
                hasMore));
    }

    private static string? Validate(RandomizeProductStockRequest request)
    {
        if (request.BatchSize is < 1 or > MaximumBatchSize)
        {
            return $"BatchSize must be between 1 and {MaximumBatchSize}.";
        }

        if (request.AfterProductId is < 0)
        {
            return "AfterProductId cannot be negative.";
        }

        if (request.MinimumAvailableStock is < 0 or > MaximumStockLimit)
        {
            return $"MinimumAvailableStock must be between 0 and {MaximumStockLimit}.";
        }

        if (request.MaximumAvailableStock is < 0 or > MaximumStockLimit)
        {
            return $"MaximumAvailableStock must be between 0 and {MaximumStockLimit}.";
        }

        return request.MinimumAvailableStock > request.MaximumAvailableStock
            ? "MinimumAvailableStock cannot exceed MaximumAvailableStock."
            : null;
    }

    private static int GenerateAvailableStock(int minimum, int maximum)
    {
        if (minimum == maximum)
        {
            return minimum;
        }

        // Squaring a uniform sample biases the generated catalog toward low and
        // medium inventory while still producing occasional high-stock products.
        var weightedSample = Math.Pow(Random.Shared.NextDouble(), 2);
        var inclusiveRange = (long)maximum - minimum + 1;
        return minimum + (int)Math.Min(
            inclusiveRange - 1,
            Math.Floor(weightedSample * inclusiveRange));
    }
}
