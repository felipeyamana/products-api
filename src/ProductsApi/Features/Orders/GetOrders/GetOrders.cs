using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Orders.Shared;

namespace ProductsApi.Features.Orders.GetOrders;

public sealed record GetOrdersQuery(
    Guid UserId,
    int Page,
    int PageSize);

public sealed class GetOrdersHandler(AppDbContext dbContext)
    : IQueryHandler<GetOrdersQuery, OrderResult<PagedOrdersDto>>
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public async Task<OrderResult<PagedOrdersDto>> Handle(
        GetOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(query);
        if (validationError is not null)
        {
            return validationError;
        }

        if (!await CustomerExistsAsync(query.UserId, cancellationToken))
        {
            return OrderResult<PagedOrdersDto>.NotFound(
                "Customer record not found.");
        }

        var page = await LoadPageAsync(query, cancellationToken);
        return OrderResult<PagedOrdersDto>.Success(page);
    }

    private static OrderResult<PagedOrdersDto>? Validate(GetOrdersQuery query)
    {
        if (query.Page < 1)
        {
            return OrderResult<PagedOrdersDto>.BadRequest(
                "Page must be greater than zero.");
        }

        if (query.PageSize is < 1 or > MaximumPageSize)
        {
            return OrderResult<PagedOrdersDto>.BadRequest(
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        return query.Page - 1 > int.MaxValue / query.PageSize
            ? OrderResult<PagedOrdersDto>.BadRequest(
                "The requested page is too large.")
            : null;
    }

    private Task<bool> CustomerExistsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                customer => customer.UserId == userId,
                cancellationToken);

    private async Task<PagedOrdersDto> LoadPageAsync(
        GetOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var orders = BuildCustomerOrdersQuery(query.UserId);
        var totalCount = await orders.CountAsync(cancellationToken);
        var rows = await LoadRowsAsync(orders, query, cancellationToken);
        var items = rows
            .Select(ToSummaryDto)
            .ToArray();

        return new PagedOrdersDto(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            CalculateTotalPages(totalCount, query.PageSize));
    }

    private IQueryable<Data.Entities.Order> BuildCustomerOrdersQuery(Guid userId) =>
        dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Customer.UserId == userId);

    private static Task<List<OrderRow>> LoadRowsAsync(
        IQueryable<Data.Entities.Order> orders,
        GetOrdersQuery query,
        CancellationToken cancellationToken) =>
        orders
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(order => new OrderRow(
                order.PublicId,
                order.Status,
                order.CurrencyCode,
                order.GrandTotal,
                order.Items.Sum(item => (int?)item.Quantity) ?? 0,
                order.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    private static OrderSummaryDto ToSummaryDto(OrderRow order) =>
        new(
            order.Id,
            order.Status.ToString(),
            order.CurrencyCode,
            order.GrandTotal,
            order.TotalQuantity,
            order.CreatedAtUtc);

    private static int CalculateTotalPages(int totalCount, int pageSize) =>
        totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

    private sealed record OrderRow(
        Guid Id,
        Data.Entities.OrderStatus Status,
        string CurrencyCode,
        decimal GrandTotal,
        int TotalQuantity,
        DateTime CreatedAtUtc);
}
