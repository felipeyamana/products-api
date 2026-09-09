using Microsoft.EntityFrameworkCore;
using ProductsApi.Data;
using ProductsApi.Data.Entities;

namespace ProductsApi.Features.Cart;

public sealed class CartService(AppDbContext db, CartLockManager lockManager)
{
    public const int MaxQuantity = 99;
    public const int MaxItems = 50;

    public async Task<CartResult> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var userError = ValidateUserId(userId);
        if (userError is not null)
            return userError;

        var cart = await db.Carts.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (cart is null)
            return CartResult.Success(CartMapper.EmptyCart);

        var products = await LoadProductsAsync(cart.Items.Select(x => x.ProductId), cancellationToken);
        return CartResult.Success(CartMapper.ToDto(cart, products));
    }

    public async Task<CartResult> SetAsync(string userId, long productId,
        SetCartItemRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateSetRequest(userId, productId, request);
        if (validationError is not null)
            return validationError;

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await lockManager.AcquireAsync(userId, cancellationToken);
            var cart = await LoadCartAsync(userId, cancellationToken);

            var versionError = ValidateVersion(cart, request.Version!.Value);
            if (versionError is not null)
                return versionError;

            cart ??= new Data.Entities.Cart { UserId = userId, Version = Guid.Empty };
            var products = await LoadProductsAsync(
                cart.Items.Select(x => x.ProductId).Append(productId), cancellationToken);

            if (!products.TryGetValue(productId, out var product) ||
                CartMapper.GetCurrentPrice(product) is not { } currentPrice)
                return CartResult.BadRequest("Product is unavailable or has no valid current price/currency.");

            var currency = CartMapper.NormalizeCurrency(currentPrice.CurrencyCode);
            if (!UsesSingleCurrency(cart, products, currency))
                return CartResult.BadRequest(
                    "All cart items must use the same currency. Remove items with a changed currency first.");

            var item = cart.Items.SingleOrDefault(x => x.ProductId == productId);
            bool changed;
            if (item is null)
            {
                var addError = AddItem(cart, productId, request.Quantity, currentPrice, currency);
                if (addError is not null)
                    return CartResult.BadRequest(addError);

                changed = true;
            }
            else
            {
                changed = UpdateQuantity(item, request.Quantity);
            }

            if (changed)
                await SaveCartAsync(cart, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return CartResult.Success(CartMapper.ToDto(cart, products));
        });
    }

    public async Task<CartResult> RemoveAsync(string userId, long productId,
        Guid? version, CancellationToken cancellationToken)
    {
        var validationError = ValidateMutation(userId, version);
        if (validationError is not null)
            return validationError;
        if (productId <= 0)
            return CartResult.BadRequest("Product ID must be positive.");

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await lockManager.AcquireAsync(userId, cancellationToken);
            var cart = await LoadCartAsync(userId, cancellationToken);

            var versionError = ValidateVersion(cart, version!.Value);
            if (versionError is not null)
                return versionError;
            if (cart is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return CartResult.Success(CartMapper.EmptyCart);
            }

            var item = cart.Items.SingleOrDefault(x => x.ProductId == productId);
            if (item is not null)
            {
                db.CartItems.Remove(item);
                cart.Items.Remove(item);
                await SaveCartAsync(cart, cancellationToken);
            }

            var products = await LoadProductsAsync(cart.Items.Select(x => x.ProductId), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CartResult.Success(CartMapper.ToDto(cart, products));
        });
    }

    public async Task<CartResult> ClearAsync(string userId, Guid? version,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateMutation(userId, version);
        if (validationError is not null)
            return validationError;

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await lockManager.AcquireAsync(userId, cancellationToken);
            var cart = await LoadCartAsync(userId, cancellationToken);

            var versionError = ValidateVersion(cart, version!.Value);
            if (versionError is not null)
                return versionError;
            if (cart is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return CartResult.Success(CartMapper.EmptyCart);
            }

            if (cart.Items.Count > 0)
            {
                db.CartItems.RemoveRange(cart.Items);
                cart.Items.Clear();
                await SaveCartAsync(cart, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return CartResult.Success(CartMapper.ToDto(cart, new Dictionary<long, Product>()));
        });
    }

    private Task<Data.Entities.Cart?> LoadCartAsync(string userId, CancellationToken cancellationToken) =>
        db.Carts.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    private Task<Dictionary<long, Product>> LoadProductsAsync(
        IEnumerable<long> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        return db.Products.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Include(x => x.Prices).ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private async Task SaveCartAsync(Data.Entities.Cart cart, CancellationToken cancellationToken)
    {
        if (db.Entry(cart).State == EntityState.Detached)
            db.Carts.Add(cart);

        cart.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? AddItem(Data.Entities.Cart cart, long productId, int quantity,
        ProductPrice currentPrice, string currency)
    {
        if (cart.Items.Count >= MaxItems)
            return "A cart can contain at most 50 distinct products.";

        var now = DateTime.UtcNow;
        cart.Items.Add(new CartItem
        {
            UserId = cart.UserId,
            ProductId = productId,
            Quantity = quantity,
            UnitPriceAtAddition = currentPrice.ActualPrice,
            CurrencyAtAddition = currency,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        return null;
    }

    private static bool UpdateQuantity(CartItem item, int quantity)
    {
        if (item.Quantity == quantity)
            return false;

        item.Quantity = quantity;
        item.UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    private static bool UsesSingleCurrency(Data.Entities.Cart cart,
        IReadOnlyDictionary<long, Product> products, string requestedCurrency)
    {
        if (cart.Items.Any(x => x.CurrencyAtAddition != requestedCurrency))
            return false;

        return cart.Items.All(item =>
            !products.TryGetValue(item.ProductId, out var product) ||
            CartMapper.GetCurrentPrice(product) is not { } price ||
            CartMapper.NormalizeCurrency(price.CurrencyCode) == requestedCurrency);
    }

    private static CartResult? ValidateSetRequest(
        string userId, long productId, SetCartItemRequest request)
    {
        var mutationError = ValidateMutation(userId, request.Version);
        if (mutationError is not null)
            return mutationError;
        if (productId <= 0)
            return CartResult.BadRequest("Product ID must be positive.");

        return request.Quantity is < 1 or > MaxQuantity
            ? CartResult.BadRequest("Quantity must be between 1 and 99.")
            : null;
    }

    private static CartResult? ValidateMutation(string userId, Guid? version)
    {
        var userError = ValidateUserId(userId);
        if (userError is not null)
            return userError;

        return version is null
            ? CartResult.BadRequest("The current cart version is required. Read the cart first.")
            : null;
    }

    private static CartResult? ValidateUserId(string userId) =>
        string.IsNullOrWhiteSpace(userId) || userId.Length > 200 || userId != userId.Trim()
            ? CartResult.Forbidden("A valid shopper identity is required.")
            : null;

    private static CartResult? ValidateVersion(Data.Entities.Cart? cart, Guid requestedVersion) =>
        requestedVersion != (cart?.Version ?? Guid.Empty)
            ? CartResult.Conflict("The cart changed. Refresh it and retry.")
            : null;

}
