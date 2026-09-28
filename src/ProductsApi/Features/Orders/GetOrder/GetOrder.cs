using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Orders.Shared;

namespace ProductsApi.Features.Orders.GetOrder;

public sealed record GetOrderQuery(Guid UserId, Guid OrderId);

public sealed class GetOrderHandler(AppDbContext dbContext)
    : IQueryHandler<GetOrderQuery, OrderResult<OrderDetailDto>>
{
    public async Task<OrderResult<OrderDetailDto>> Handle(
        GetOrderQuery query,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(
            query.UserId,
            query.OrderId,
            cancellationToken);

        return order is null
            ? OrderResult<OrderDetailDto>.NotFound("Order not found.")
            : OrderResult<OrderDetailDto>.Success(
                OrderMapper.ToDetailDto(order));
    }

    private Task<Order?> LoadOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken) =>
        dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order =>
                    order.PublicId == orderId &&
                    order.Customer.UserId == userId,
                cancellationToken);
}
