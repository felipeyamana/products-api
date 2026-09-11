namespace ProductsApi.Features.Categories.GetCategories;

public sealed record CategoryDto(
    int Id,
    string Name,
    int? ParentCategoryId);
