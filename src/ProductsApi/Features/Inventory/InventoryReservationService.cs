using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProductsApi.Data;
using ProductsApi.Data.Entities;

namespace ProductsApi.Features.Inventory;

public sealed class InventoryReservationService(
    AppDbContext dbContext,
    IOptions<InventoryOptions> options,
    TimeProvider timeProvider)
{
    public async Task<InventoryReservationResult> EnsureReservedAsync(
        Order order,
        bool allowExpiredReplacement,
        CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var quantities = GetOrderQuantities(order);
        if (quantities.Error is not null)
        {
            return InventoryReservationResult.Failure(quantities.Error);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await dbContext.InventoryReservations
            .AsNoTracking()
            .Where(reservation => reservation.OrderId == order.Id)
            .OrderBy(reservation => reservation.ProductId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            var matchesOrder = existing.Count == quantities.Items!.Count &&
                existing.All(reservation =>
                    quantities.Items.TryGetValue(reservation.ProductId, out var quantity) &&
                    quantity == reservation.Quantity);
            if (!matchesOrder)
            {
                return InventoryReservationResult.Failure(
                    "The order has an inconsistent inventory reservation.");
            }

            var expiresAtUtc = existing.Min(reservation => reservation.ExpiresAtUtc);
            if (expiresAtUtc > utcNow)
            {
                return InventoryReservationResult.Success([], expiresAtUtc);
            }

            if (!allowExpiredReplacement)
            {
                return InventoryReservationResult.Failure(
                    "This checkout reservation expired. Refresh the order status before retrying.");
            }

            await dbContext.InventoryReservations
                .Where(reservation => reservation.OrderId == order.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var reservationMinutes = options.Value.ReservationMinutes;
        if (reservationMinutes is <
                InventoryOptions.MinimumReservationMinutes or
            > InventoryOptions.MaximumReservationMinutes)
        {
            return InventoryReservationResult.Failure(
                $"Inventory:ReservationMinutes must be between " +
                $"{InventoryOptions.MinimumReservationMinutes} and " +
                $"{InventoryOptions.MaximumReservationMinutes}.");
        }

        var newExpiresAtUtc = utcNow.AddMinutes(reservationMinutes);
        var changedProductIds = new List<long>(quantities.Items!.Count);

        foreach (var item in quantities.Items.OrderBy(item => item.Key))
        {
            var productId = item.Key;
            var quantity = item.Value;

            // Updating the inventory row takes a per-product update lock. A
            // competing reservation waits, then re-evaluates the active lease
            // sum after this transaction commits.
            var affected = await dbContext.ProductInventories
                .Where(inventory =>
                    inventory.ProductId == productId &&
                    inventory.OnHand -
                    (dbContext.InventoryReservations
                        .Where(reservation =>
                            reservation.ProductId == productId &&
                            reservation.ExpiresAtUtc > utcNow)
                        .Sum(reservation => (int?)reservation.Quantity) ?? 0) >=
                    quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        inventory => inventory.UpdatedAtUtc,
                        utcNow),
                    cancellationToken);

            if (affected != 1)
            {
                return InventoryReservationResult.Failure(
                    $"Insufficient stock for product {productId}.");
            }

            dbContext.InventoryReservations.Add(new InventoryReservation
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                CreatedAtUtc = utcNow,
                ExpiresAtUtc = newExpiresAtUtc
            });
            changedProductIds.Add(productId);
        }

        return InventoryReservationResult.Success(
            changedProductIds,
            newExpiresAtUtc);
    }

    public async Task<IReadOnlyList<long>> ConsumeAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.OrderId == order.Id)
            .OrderBy(reservation => reservation.ProductId)
            .ToListAsync(cancellationToken);

        if (reservations.Count == 0)
        {
            return await ConsumeWithoutReservationAsync(order, cancellationToken);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var reservation in reservations)
        {
            var affected = await dbContext.ProductInventories
                .Where(inventory =>
                    inventory.ProductId == reservation.ProductId &&
                    inventory.OnHand -
                    (dbContext.InventoryReservations
                        .Where(other =>
                            other.ProductId == reservation.ProductId &&
                            other.OrderId != order.Id &&
                            other.ExpiresAtUtc > utcNow)
                        .Sum(other => (int?)other.Quantity) ?? 0) >=
                    reservation.Quantity)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            inventory => inventory.OnHand,
                            inventory => inventory.OnHand - reservation.Quantity)
                        .SetProperty(
                            inventory => inventory.UpdatedAtUtc,
                            utcNow),
                    cancellationToken);

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Paid order {order.PublicId} cannot be allocated from current stock.");
            }
        }

        dbContext.InventoryReservations.RemoveRange(reservations);
        return reservations.Select(reservation => reservation.ProductId).ToArray();
    }

    public async Task<IReadOnlyList<long>> ReleaseAsync(
        long orderId,
        CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.OrderId == orderId)
            .OrderBy(reservation => reservation.ProductId)
            .ToListAsync(cancellationToken);

        dbContext.InventoryReservations.RemoveRange(reservations);
        return reservations.Select(reservation => reservation.ProductId).ToArray();
    }

    private async Task<IReadOnlyList<long>> ConsumeWithoutReservationAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var quantities = GetOrderQuantities(order);
        if (quantities.Error is not null)
        {
            throw new InvalidOperationException(quantities.Error);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in quantities.Items!.OrderBy(item => item.Key))
        {
            var productId = item.Key;
            var quantity = item.Value;
            var affected = await dbContext.ProductInventories
                .Where(inventory =>
                    inventory.ProductId == productId &&
                    inventory.OnHand -
                    (dbContext.InventoryReservations
                        .Where(reservation =>
                            reservation.ProductId == productId &&
                            reservation.ExpiresAtUtc > utcNow)
                        .Sum(reservation => (int?)reservation.Quantity) ?? 0) >=
                    quantity)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            inventory => inventory.OnHand,
                            inventory => inventory.OnHand - quantity)
                        .SetProperty(
                            inventory => inventory.UpdatedAtUtc,
                            utcNow),
                    cancellationToken);

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Paid order {order.PublicId} cannot be allocated from current stock.");
            }
        }

        return quantities.Items!.Keys.ToArray();
    }

    private static OrderQuantities GetOrderQuantities(Order order)
    {
        if (order.Items.Count == 0 || order.Items.Any(item => item.ProductId is null))
        {
            return OrderQuantities.Failure(
                "Every order item must reference a current product to reserve stock.");
        }

        var items = order.Items
            .GroupBy(item => item.ProductId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.Quantity));
        return OrderQuantities.Success(items);
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Inventory reservations must be changed inside a database transaction.");
        }
    }

    private sealed record OrderQuantities(
        IReadOnlyDictionary<long, int>? Items,
        string? Error)
    {
        public static OrderQuantities Success(IReadOnlyDictionary<long, int> items) =>
            new(items, null);

        public static OrderQuantities Failure(string error) =>
            new(null, error);
    }
}

public sealed record InventoryReservationResult(
    bool IsSuccess,
    IReadOnlyList<long> ChangedProductIds,
    DateTime? ExpiresAtUtc,
    string? Error)
{
    public static InventoryReservationResult Success(
        IReadOnlyList<long> productIds,
        DateTime expiresAtUtc) =>
        new(true, productIds, expiresAtUtc, null);

    public static InventoryReservationResult Failure(string error) =>
        new(false, [], null, error);
}
