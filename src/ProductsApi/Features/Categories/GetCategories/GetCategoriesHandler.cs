using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;

namespace ProductsApi.Features.Categories.GetCategories;

public sealed class GetCategoriesHandler(AppDbContext dbContext)
    : IQueryHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryDto(
                category.Id,
                category.Name,
                category.ParentCategoryId))
            .ToListAsync(cancellationToken);
    }
}
