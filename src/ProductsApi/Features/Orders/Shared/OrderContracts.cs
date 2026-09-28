using System.ComponentModel.DataAnnotations;
using ProductsApi.Data.Entities;

namespace ProductsApi.Features.Orders.Shared;

public sealed record CreateOrderRequest(
    Guid AddressId,
    [Required] Guid? CartVersion);

public sealed record OrderSummaryDto(
    Guid Id,
    string Status,
    string CurrencyCode,
    decimal GrandTotal,
    int TotalQuantity,
    DateTime CreatedAtUtc);

public sealed record PagedOrdersDto(
    IReadOnlyList<OrderSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record OrderItemDto(
    long? ProductId,
    string ProductName,
    string? ProductExternalId,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineTotal);

public sealed record OrderShippingAddressDto(
    string RecipientName,
    string? PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string CountryCode);

public sealed record OrderDetailDto(
    Guid Id,
    string Status,
    string CustomerEmail,
    OrderShippingAddressDto ShippingAddress,
    string CurrencyCode,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal ShippingTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<OrderItemDto> Items);

public enum OrderFailureKind
{
    None,
    BadRequest,
    NotFound,
    Conflict
}

public sealed record OrderResult<T>(
    T? Value,
    OrderFailureKind Failure,
    string? Error)
{
    public bool IsSuccess => Failure == OrderFailureKind.None;

    public static OrderResult<T> Success(T value) =>
        new(value, OrderFailureKind.None, null);

    public static OrderResult<T> BadRequest(string error) =>
        new(default, OrderFailureKind.BadRequest, error);

    public static OrderResult<T> NotFound(string error) =>
        new(default, OrderFailureKind.NotFound, error);

    public static OrderResult<T> Conflict(string error) =>
        new(default, OrderFailureKind.Conflict, error);
}

internal static class OrderMapper
{
    public static OrderDetailDto ToDetailDto(Order order) =>
        new(
            order.PublicId,
            order.Status.ToString(),
            order.CustomerEmail,
            new OrderShippingAddressDto(
                order.RecipientName,
                order.ShippingPhoneNumber,
                order.ShippingAddressLine1,
                order.ShippingAddressLine2,
                order.ShippingCity,
                order.ShippingRegion,
                order.ShippingPostalCode,
                order.ShippingCountryCode),
            order.CurrencyCode,
            order.Subtotal,
            order.DiscountTotal,
            order.ShippingTotal,
            order.TaxTotal,
            order.GrandTotal,
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemDto(
                    item.ProductId,
                    item.ProductName,
                    item.ProductExternalId,
                    item.Quantity,
                    item.UnitPrice,
                    item.DiscountAmount,
                    item.LineTotal))
                .ToArray());
}
