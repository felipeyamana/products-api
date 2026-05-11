using Application.Common;
using Application.Products.Dtos;
using Application.Products.Factories;
using Application.Products.Interfaces;
using Application.Products.Repositories;

namespace Application.Products.Services;

public class ProductService(IProductRepository productRepository, IProductFactory productFactory)
    : IProductService
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IProductFactory _productFactory = productFactory;

    public async Task<Result<PagedProductsDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var pagingError = ProductPaging.NormalizePage(ref pageNumber, ref pageSize);
        if (pagingError is not null)
            return Result<PagedProductsDto>.Fail(pagingError);

        var (products, totalCount) = await _productRepository.GetPagedAsync(pageNumber, pageSize, cancellationToken);

        var items = products.Select(_productFactory.ToDto).ToList();
        var totalPages = ProductPaging.TotalPages(totalCount, pageSize);

        return Result<PagedProductsDto>.Ok(new PagedProductsDto(items, pageNumber, pageSize, totalCount, totalPages));
    }

    public async Task<Result<ProductDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return Result<ProductDto>.Fail("Product not found.");

        return Result<ProductDto>.Ok(_productFactory.ToDto(product));
    }

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ProductRequestValidator.ValidateCreate(request);
        if (validationError is not null)
            return Result<ProductDto>.Fail(validationError);

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await _productRepository.ExternalProductIdExistsAsync(request.ExternalProductId, null, cancellationToken))
            return Result<ProductDto>.Fail($"A product with external id '{request.ExternalProductId}' already exists.");

        var utcNow = DateTime.UtcNow;
        var product = _productFactory.CreateFrom(request, utcNow);

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        var created = await _productRepository.GetByIdAsync(product.Id, cancellationToken);
        if (created is null)
            return Result<ProductDto>.Fail("Product was not persisted.");

        return Result<ProductDto>.Ok(_productFactory.ToDto(created));
    }

    public async Task<Result<ProductDto>> ReplaceAsync(long id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ProductRequestValidator.ValidateReplace(request);
        if (validationError is not null)
            return Result<ProductDto>.Fail(validationError);

        var product = await _productRepository.GetByIdTrackedAsync(id, cancellationToken);
        if (product is null)
            return Result<ProductDto>.Fail("Product not found.");

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await _productRepository.ExternalProductIdExistsAsync(request.ExternalProductId, id, cancellationToken))
            return Result<ProductDto>.Fail($"A product with external id '{request.ExternalProductId}' already exists.");

        _productFactory.ApplyFullReplace(product, request, DateTime.UtcNow);

        await _productRepository.SaveChangesAsync(cancellationToken);

        var updated = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (updated is null)
            return Result<ProductDto>.Fail("Product not found.");

        return Result<ProductDto>.Ok(_productFactory.ToDto(updated));
    }

    public async Task<Result<ProductDto>> PatchAsync(long id, PatchProductRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ProductRequestValidator.ValidatePatch(request);
        if (validationError is not null)
            return Result<ProductDto>.Fail(validationError);

        var product = await _productRepository.GetByIdTrackedAsync(id, cancellationToken);
        if (product is null)
            return Result<ProductDto>.Fail("Product not found.");

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await _productRepository.ExternalProductIdExistsAsync(request.ExternalProductId, id, cancellationToken))
            return Result<ProductDto>.Fail($"A product with external id '{request.ExternalProductId}' already exists.");

        _productFactory.ApplyPatch(product, request, DateTime.UtcNow);

        await _productRepository.SaveChangesAsync(cancellationToken);

        var updated = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (updated is null)
            return Result<ProductDto>.Fail("Product not found.");

        return Result<ProductDto>.Ok(_productFactory.ToDto(updated));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdTrackedAsync(id, cancellationToken);
        if (product is null)
            return Result<bool>.Fail("Product not found.");

        _productRepository.Remove(product);
        await _productRepository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
