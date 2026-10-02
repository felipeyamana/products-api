using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Products.Shared;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.GetPagedProducts;

public sealed record GetPagedProductsQuery(
    int PageNumber,
    int PageSize,
    string? Search = null,
    int? CategoryId = null,
    int? SubCategoryId = null,
    IReadOnlyCollection<string>? Brands = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? MinRating = null,
    string? Sort = null);

public sealed class GetPagedProductsHandler(AppDbContext dbContext)
    : IQueryHandler<GetPagedProductsQuery, Result<PagedProductsDto>>
{
    public async Task<Result<PagedProductsDto>> Handle(GetPagedProductsQuery query, CancellationToken cancellationToken)
    {
        var validation = Validate(query);
        if (!validation.IsSuccess)
            return Result<PagedProductsDto>.Fail(validation.Error!);

        var options = validation.Value!;
        var baseProducts = ApplyBaseFilters(dbContext.Products.AsNoTracking(), query, options);
        var products = ApplyRatingFilter(
            ApplyPriceFilter(
                ApplyBrandFilter(baseProducts, options.Brands),
                query),
            query.MinRating);

        var ordered = ApplySort(products, options.Sort);

        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((options.PageNumber - 1) * options.PageSize)
            .Take(options.PageSize)
            .Select(ProductMapper.ToDtoProjection)
            .ToListAsync(cancellationToken);

        var totalPages = ProductPaging.TotalPages(totalCount, options.PageSize);
        var facets = await GetFacets(baseProducts, query, options.Brands, cancellationToken);

        return Result<PagedProductsDto>.Ok(
            new PagedProductsDto(items, options.PageNumber, options.PageSize, totalCount, totalPages, facets));
    }

    private static Result<ValidatedCatalogQuery> Validate(GetPagedProductsQuery query)
    {
        var pageNumber = query.PageNumber;
        var pageSize = query.PageSize;
        var pagingError = ProductPaging.NormalizePage(ref pageNumber, ref pageSize);
        if (pagingError is not null)
            return Result<ValidatedCatalogQuery>.Fail(pagingError);

        var search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim();
        if (search?.Length > ProductConstraints.MaxSearchLength)
            return Result<ValidatedCatalogQuery>.Fail(
                $"Search must not exceed {ProductConstraints.MaxSearchLength} characters.");

        if (query.CategoryId is <= 0)
            return Result<ValidatedCatalogQuery>.Fail("CategoryId must be greater than zero.");

        if (query.SubCategoryId is <= 0)
            return Result<ValidatedCatalogQuery>.Fail("SubCategoryId must be greater than zero.");

        if (query.MinPrice is < 0 or > ProductConstraints.MaxCatalogPrice)
            return Result<ValidatedCatalogQuery>.Fail(
                $"MinPrice must be between 0 and {ProductConstraints.MaxCatalogPrice}.");

        if (query.MaxPrice is < 0 or > ProductConstraints.MaxCatalogPrice)
            return Result<ValidatedCatalogQuery>.Fail(
                $"MaxPrice must be between 0 and {ProductConstraints.MaxCatalogPrice}.");

        if (query.MinPrice > query.MaxPrice)
            return Result<ValidatedCatalogQuery>.Fail("MinPrice must not be greater than MaxPrice.");

        if (query.MinRating is < 0 or > 5)
            return Result<ValidatedCatalogQuery>.Fail("MinRating must be between 0 and 5.");

        var brands = NormalizeBrands(query.Brands);
        if (brands.Length > ProductConstraints.MaxBrandFilters)
            return Result<ValidatedCatalogQuery>.Fail(
                $"No more than {ProductConstraints.MaxBrandFilters} brands can be requested.");

        if (brands.Any(brand => brand.Length > ProductConstraints.MaxBrandLength))
            return Result<ValidatedCatalogQuery>.Fail(
                $"Each brand must not exceed {ProductConstraints.MaxBrandLength} characters.");

        var sort = string.IsNullOrWhiteSpace(query.Sort)
            ? ProductCatalogSort.NameAscending
            : query.Sort.Trim().ToLowerInvariant();
        if (!ProductCatalogSort.SupportedValues.Contains(sort))
            return Result<ValidatedCatalogQuery>.Fail(
                $"Sort must be one of: {string.Join(", ", ProductCatalogSort.SupportedValues)}.");

        return Result<ValidatedCatalogQuery>.Ok(
            new ValidatedCatalogQuery(pageNumber, pageSize, search, brands, sort));
    }

    private static IQueryable<Product> ApplyBaseFilters(
        IQueryable<Product> products,
        GetPagedProductsQuery query,
        ValidatedCatalogQuery options)
    {
        if (options.Search is not null)
        {
            products = products.Where(product =>
                EF.Functions.FreeText(product.Name, options.Search) ||
                EF.Functions.FreeText(product.Brand!, options.Search) ||
                EF.Functions.FreeText(product.Description!, options.Search));
        }

        if (query.CategoryId is not null)
            products = products.Where(product => product.CategoryId == query.CategoryId);

        if (query.SubCategoryId is not null)
            products = products.Where(product => product.SubCategoryId == query.SubCategoryId);

        return products;
    }

    private static IQueryable<Product> ApplyBrandFilter(IQueryable<Product> products, string[] brands) =>
        brands.Length == 0
            ? products
            : products.Where(product => product.Brand != null && brands.Contains(product.Brand));

    private static IQueryable<Product> ApplyPriceFilter(
        IQueryable<Product> products,
        GetPagedProductsQuery query)
    {
        if (query.MinPrice is not null)
        {
            products = products.Where(product => product.Prices
                .OrderByDescending(price => price.CapturedAt)
                .ThenByDescending(price => price.Id)
                .Select(price => (decimal?)price.ActualPrice)
                .FirstOrDefault() >= query.MinPrice);
        }

        if (query.MaxPrice is not null)
        {
            products = products.Where(product => product.Prices
                .OrderByDescending(price => price.CapturedAt)
                .ThenByDescending(price => price.Id)
                .Select(price => (decimal?)price.ActualPrice)
                .FirstOrDefault() <= query.MaxPrice);
        }

        return products;
    }

    private static IQueryable<Product> ApplyRatingFilter(
        IQueryable<Product> products,
        decimal? minRating) =>
        minRating is null
            ? products
            : products.Where(product => product.AverageRating >= minRating);

    private static async Task<ProductFacetsDto> GetFacets(
        IQueryable<Product> baseProducts,
        GetPagedProductsQuery query,
        string[] brands,
        CancellationToken cancellationToken)
    {
        var brandFacetProducts = ApplyRatingFilter(
            ApplyPriceFilter(baseProducts, query),
            query.MinRating);
        var brandFacets = await brandFacetProducts
            .Where(product => product.Brand != null && product.Brand != "")
            .GroupBy(product => product.Brand!)
            .Select(group => new ProductBrandFacetDto(group.Key, group.Count()))
            .OrderBy(facet => facet.Brand)
            .ToListAsync(cancellationToken);

        var priceFacetProducts = ApplyRatingFilter(
            ApplyBrandFilter(baseProducts, brands),
            query.MinRating);
        var priceRange = await priceFacetProducts
            .Select(product => product.Prices
                .OrderByDescending(price => price.CapturedAt)
                .ThenByDescending(price => price.Id)
                .Select(price => (decimal?)price.ActualPrice)
                .FirstOrDefault())
            .Where(price => price != null)
            .GroupBy(_ => 1)
            .Select(group => new PriceRange(group.Min(), group.Max()))
            .SingleOrDefaultAsync(cancellationToken);

        var ratingFacetProducts = ApplyPriceFilter(
            ApplyBrandFilter(baseProducts, brands),
            query);
        var ratingCounts = await ratingFacetProducts
            .Where(product => product.AverageRating != null)
            .GroupBy(_ => 1)
            .Select(group => new RatingCounts(
                group.Count(product => product.AverageRating >= 4m),
                group.Count(product => product.AverageRating >= 3m),
                group.Count(product => product.AverageRating >= 2m),
                group.Count(product => product.AverageRating >= 1m)))
            .SingleOrDefaultAsync(cancellationToken);

        return new ProductFacetsDto(
            priceRange?.MinPrice,
            priceRange?.MaxPrice,
            brandFacets,
            [
                new ProductRatingFacetDto(4m, ratingCounts?.FourAndUp ?? 0),
                new ProductRatingFacetDto(3m, ratingCounts?.ThreeAndUp ?? 0),
                new ProductRatingFacetDto(2m, ratingCounts?.TwoAndUp ?? 0),
                new ProductRatingFacetDto(1m, ratingCounts?.OneAndUp ?? 0)
            ]);
    }

    private static string[] NormalizeBrands(IReadOnlyCollection<string>? brands) =>
        brands is null
            ? []
            : brands
                .SelectMany(brand => brand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(brand => brand.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static IOrderedQueryable<Product> ApplySort(IQueryable<Product> products, string sort) =>
        sort switch
        {
            ProductCatalogSort.NameDescending => products
                .OrderByDescending(product => product.Name)
                .ThenByDescending(product => product.Id),
            ProductCatalogSort.PriceAscending => products
                .OrderBy(product => !product.Prices.Any())
                .ThenBy(product => product.Prices
                    .OrderByDescending(price => price.CapturedAt)
                    .ThenByDescending(price => price.Id)
                    .Select(price => (decimal?)price.ActualPrice)
                    .FirstOrDefault())
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Id),
            ProductCatalogSort.PriceDescending => products
                .OrderBy(product => !product.Prices.Any())
                .ThenByDescending(product => product.Prices
                    .OrderByDescending(price => price.CapturedAt)
                    .ThenByDescending(price => price.Id)
                    .Select(price => (decimal?)price.ActualPrice)
                    .FirstOrDefault())
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Id),
            ProductCatalogSort.RatingDescending => products
                .OrderBy(product => product.AverageRating == null)
                .ThenByDescending(product => product.AverageRating)
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Id),
            ProductCatalogSort.Newest => products
                .OrderByDescending(product => product.CreatedAt)
                .ThenByDescending(product => product.Id),
            _ => products
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Id)
        };

    private sealed record ValidatedCatalogQuery(
        int PageNumber,
        int PageSize,
        string? Search,
        string[] Brands,
        string Sort);

    private sealed record PriceRange(decimal? MinPrice, decimal? MaxPrice);

    private sealed record RatingCounts(
        int FourAndUp,
        int ThreeAndUp,
        int TwoAndUp,
        int OneAndUp);
}
