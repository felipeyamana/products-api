using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Cart;
using ProductsApi.Features.Orders.Shared;

namespace ProductsApi.Features.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    Guid UserId,
    string CartUserId,
    CreateOrderRequest Request);

public sealed class CreateOrderHandler(
    AppDbContext dbContext,
    CartLockManager cartLockManager)
    : ICommandHandler<CreateOrderCommand, OrderResult<OrderDetailDto>>
{
    public async Task<OrderResult<OrderDetailDto>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(command.Request);
        if (validationError is not null)
        {
            return validationError;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(
                () => CreateInTransactionAsync(command, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            return CartChangedConflict();
        }
    }

    private async Task<OrderResult<OrderDetailDto>> CreateInTransactionAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var customer = await LoadCustomerAsync(
            command.UserId,
            cancellationToken);

        if (customer is null)
        {
            return OrderResult<OrderDetailDto>.NotFound(
                "Customer record not found.");
        }

        var address = await LoadAddressAsync(
            customer.Id,
            command.Request.AddressId,
            cancellationToken);

        await using var transaction = await cartLockManager.AcquireAsync(
            command.CartUserId,
            cancellationToken);
        var cartVersion = command.Request.CartVersion!.Value;
        var existingOrder = await LoadExistingOrderAsync(
            customer.Id,
            cartVersion,
            cancellationToken);

        if (existingOrder is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Success(existingOrder);
        }

        var cart = await LoadCartAsync(
            command.CartUserId,
            cancellationToken);
        var cartError = ValidateCart(cart, cartVersion);
        if (cartError is not null)
        {
            return cartError;
        }

        if (address is null)
        {
            return OrderResult<OrderDetailDto>.NotFound(
                "Shipping address not found.");
        }

        var pricing = await PriceCartAsync(
            cart!,
            cancellationToken);
        if (pricing.Error is not null)
        {
            return OrderResult<OrderDetailDto>.BadRequest(pricing.Error);
        }

        var order = BuildOrder(
            customer,
            address,
            pricing.Value!,
            cartVersion);

        await PersistOrderAndClearCartAsync(
            order,
            cart!,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Success(order);
    }

    private static OrderResult<OrderDetailDto>? Validate(
        CreateOrderRequest request)
    {
        if (request.AddressId == Guid.Empty)
        {
            return OrderResult<OrderDetailDto>.BadRequest(
                "A shipping address is required.");
        }

        return request.CartVersion is null
            ? OrderResult<OrderDetailDto>.BadRequest(
                "The current cart version is required.")
            : null;
    }

    private Task<CustomerContext?> LoadCustomerAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.UserId == userId)
            .Join(
                dbContext.Users.AsNoTracking(),
                customer => customer.UserId,
                user => (Guid?)user.Id,
                (customer, user) => new CustomerContext(
                    customer.Id,
                    user.Email!,
                    customer.PhoneNumberE164,
                    customer.PhoneRegionCode))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Order?> LoadExistingOrderAsync(
        long customerId,
        Guid cartVersion,
        CancellationToken cancellationToken) =>
        dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order =>
                    order.CustomerId == customerId &&
                    order.CheckoutCartVersion == cartVersion,
                cancellationToken);

    private Task<Data.Entities.Cart?> LoadCartAsync(
        string cartUserId,
        CancellationToken cancellationToken) =>
        dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.UserId == cartUserId,
                cancellationToken);

    private static OrderResult<OrderDetailDto>? ValidateCart(
        Data.Entities.Cart? cart,
        Guid expectedVersion)
    {
        if (cart is null || cart.Items.Count == 0)
        {
            return OrderResult<OrderDetailDto>.BadRequest(
                "The cart is empty.");
        }

        return cart.Version != expectedVersion
            ? CartChangedConflict()
            : null;
    }

    private Task<CustomerAddress?> LoadAddressAsync(
        long customerId,
        Guid addressId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerAddresses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                address =>
                    address.PublicId == addressId &&
                    address.CustomerId == customerId,
                cancellationToken);

    private async Task<PricingResult> PriceCartAsync(
        Data.Entities.Cart cart,
        CancellationToken cancellationToken)
    {
        var products = await LoadProductsAsync(
            cart.Items.Select(item => item.ProductId),
            cancellationToken);
        var pricedItems = new List<PricedCartItem>();

        foreach (var cartItem in cart.Items)
        {
            if (!TryPriceItem(cartItem, products, out var pricedItem))
            {
                return PricingResult.Failure(
                    "One or more cart products are no longer available.");
            }

            pricedItems.Add(pricedItem!);
        }

        var currencies = pricedItems
            .Select(item => CartMapper.NormalizeCurrency(item.Price.CurrencyCode))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (currencies.Length != 1)
        {
            return PricingResult.Failure(
                "All order items must use the same currency.");
        }

        var subtotal = pricedItems.Sum(
            item => item.Price.ActualPrice * item.CartItem.Quantity);
        return PricingResult.Success(
            new PricedCart(currencies[0], subtotal, pricedItems));
    }

    private Task<Dictionary<long, Product>> LoadProductsAsync(
        IEnumerable<long> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();

        return dbContext.Products
            .AsNoTracking()
            .Where(product => ids.Contains(product.Id))
            .Include(product => product.Prices)
            .Include(product => product.Inventory)
            .ToDictionaryAsync(product => product.Id, cancellationToken);
    }

    private static bool TryPriceItem(
        CartItem cartItem,
        IReadOnlyDictionary<long, Product> products,
        out PricedCartItem? pricedItem)
    {
        if (!products.TryGetValue(cartItem.ProductId, out var product) ||
            CartMapper.GetCurrentPrice(product) is not { } price ||
            !CartMapper.HasSufficientStock(product, cartItem.Quantity))
        {
            pricedItem = null;
            return false;
        }

        pricedItem = new PricedCartItem(cartItem, product, price);
        return true;
    }

    private static Order BuildOrder(
        CustomerContext customer,
        CustomerAddress address,
        PricedCart pricing,
        Guid cartVersion)
    {
        var utcNow = DateTime.UtcNow;

        return new Order
        {
            CustomerId = customer.Id,
            CheckoutCartVersion = cartVersion,
            Status = OrderStatus.Pending,
            CustomerEmail = customer.Email,
            RecipientName = address.RecipientName,
            ShippingPhoneNumber = customer.PhoneNumber,
            ShippingPhoneRegionCode = customer.PhoneRegionCode,
            ShippingAddressLine1 = address.AddressLine1,
            ShippingAddressLine2 = address.AddressLine2,
            ShippingCity = address.City,
            ShippingRegion = address.Region,
            ShippingPostalCode = address.PostalCode,
            ShippingCountryCode = address.CountryCode,
            CurrencyCode = pricing.Currency,
            Subtotal = pricing.Subtotal,
            DiscountTotal = 0,
            ShippingTotal = 0,
            TaxTotal = 0,
            GrandTotal = pricing.Subtotal,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            Items = pricing.Items
                .Select(BuildOrderItem)
                .ToList()
        };
    }

    private static OrderItem BuildOrderItem(PricedCartItem item)
    {
        var lineTotal =
            item.Price.ActualPrice * item.CartItem.Quantity;

        return new OrderItem
        {
            ProductId = item.Product.Id,
            ProductName = item.Product.Name,
            ProductExternalId = item.Product.ExternalProductId,
            Quantity = item.CartItem.Quantity,
            UnitPrice = item.Price.ActualPrice,
            DiscountAmount = 0,
            LineTotal = lineTotal
        };
    }

    private async Task PersistOrderAndClearCartAsync(
        Order order,
        Data.Entities.Cart cart,
        CancellationToken cancellationToken)
    {
        dbContext.Orders.Add(order);
        dbContext.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();
        cart.Version = Guid.NewGuid();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OrderResult<OrderDetailDto> Success(Order order) =>
        OrderResult<OrderDetailDto>.Success(
            OrderMapper.ToDetailDto(order));

    private static OrderResult<OrderDetailDto> CartChangedConflict() =>
        OrderResult<OrderDetailDto>.Conflict(
            "The cart changed. Refresh it and retry.");

    private sealed record CustomerContext(
        long Id,
        string Email,
        string? PhoneNumber,
        string? PhoneRegionCode);

    private sealed record PricedCart(
        string Currency,
        decimal Subtotal,
        IReadOnlyList<PricedCartItem> Items);

    private sealed record PricedCartItem(
        CartItem CartItem,
        Product Product,
        ProductPrice Price);

    private sealed record PricingResult(
        PricedCart? Value,
        string? Error)
    {
        public static PricingResult Success(PricedCart value) =>
            new(value, null);

        public static PricingResult Failure(string error) =>
            new(null, error);
    }
}
