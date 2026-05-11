using Application.Products;
using Application.Products.Dtos;
using Application.Products.Exceptions;
using Application.Products.Factories;
using Application.Products.Interfaces;
using Application.Products.Repositories;

namespace Application.Products.Services;

public class ProductService(IProductRepository productRepository, IProductFactory productFactory)
    : IProductService
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IProductFactory _productFactory = productFactory;

    public async Task<PagedProductsDto> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        ProductPaging.NormalizePage(ref pageNumber, ref pageSize);

        var (products, totalCount) = await _productRepository.GetPagedAsync(pageNumber, pageSize, cancellationToken);

        var items = products.Select(_productFactory.ToDto).ToList();
        var totalPages = ProductPaging.TotalPages(totalCount, pageSize);

        return new PagedProductsDto(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<ProductDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product is null ? null : _productFactory.ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        ProductRequestValidator.ValidateCreate(request);

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await _productRepository.ExternalProductIdExistsAsync(request.ExternalProductId, null, cancellationToken))
            throw new DuplicateExternalProductIdException(request.ExternalProductId);

        var utcNow = DateTime.UtcNow;
        var product = _productFactory.CreateFrom(request, utcNow);

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        var created = await _productRepository.GetByIdAsync(product.Id, cancellationToken)
            ?? throw new InvalidOperationException("Product was not persisted.");

        return _productFactory.ToDto(created);
    }

    public async Task<ProductDto?> ReplaceAsync(long id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        ProductRequestValidator.ValidateReplace(request);

        var product = await _productRepository.GetByIdTrackedAsync(id, cancellationToken);
        if (product is null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await _productRepository.ExternalProductIdExistsAsync(request.ExternalProductId, id, cancellationToken))
            throw new DuplicateExternalProductIdException(request.ExternalProductId);

        _productFactory.ApplyFullReplace(product, request, DateTime.UtcNow);

        await _productRepository.SaveChangesAsync(cancellationToken);

        var updated = await _productRepository.GetByIdAsync(id, cancellationToken);
        return updated is null ? null : _productFactory.ToDto(updated);
    }

    public async Task<ProductDto?> PatchAsync(long id, PatchProductRequest request, CancellationToken cancellationToken = default)
    {
        ProductRequestValidator.ValidatePatch(request);

        var product = await _productRepository.GetByIdTrackedAsync(id, cancellationToken);
        if (product is null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await _productRepository.ExternalProductIdExistsAsync(request.ExternalProductId, id, cancellationToken))
            throw new DuplicateExternalProductIdException(request.ExternalProductId);

        _productFactory.ApplyPatch(product, request, DateTime.UtcNow);

        await _productRepository.SaveChangesAsync(cancellationToken);

        var updated = await _productRepository.GetByIdAsync(id, cancellationToken);
        return updated is null ? null : _productFactory.ToDto(updated);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdTrackedAsync(id, cancellationToken);
        if (product is null)
            return false;

        _productRepository.Remove(product);
        await _productRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
