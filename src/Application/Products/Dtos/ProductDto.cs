namespace Application.Products.Dtos;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string? Description);
