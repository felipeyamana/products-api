namespace ProductsApi.Features.Products.RandomizeProductStock;

public sealed record RandomizeProductStockRequest(
    int BatchSize = 100,
    long? AfterProductId = null,
    int MinimumAvailableStock = 0,
    int MaximumAvailableStock = 500);

public sealed record RandomizedProductStockDto(
    long ProductId,
    int OnHand,
    int Reserved,
    int Available);

public sealed record RandomizeProductStockDto(
    IReadOnlyList<RandomizedProductStockDto> Items,
    int AssignedCount,
    long? NextAfterProductId,
    bool HasMore);
