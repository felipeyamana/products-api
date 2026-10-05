using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Customers.Shared;
using ProductsApi.Features.Orders.Shared;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using ProductsApi.Features.Inventory;
using ProductsApi.Features.Orders.CreateCheckoutSession;
using ProductsApi.Features.Products.Shared;
using ProductsApi.Payments;
using Stripe.Checkout;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class CustomerOrderTests(MsSqlContainerFixture fixture) : IAsyncLifetime
{
    private TestData data = null!;

    public async Task InitializeAsync()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await fixture.ResetDatabaseAsync();
        data = await CreateTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CustomerProfile_CanBeReadAndUpdatedWithConcurrency()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(
            client,
            Token(data.UserId, "customer:read customer:write", "CustomerUser"));

        var profile = await client.GetFromJsonAsync<CustomerProfileDto>(
            "/api/customers/me");
        Assert.NotNull(profile);
        Assert.Equal(data.Email, profile.Email);

        var incompletePhoneResponse = await client.PutAsJsonAsync(
            "/api/customers/me",
            new UpdateCustomerProfileRequest(
                "Felipe",
                "Customer",
                "+5511999990000",
                null,
                profile.Version));
        Assert.Equal(HttpStatusCode.BadRequest, incompletePhoneResponse.StatusCode);

        var update = new UpdateCustomerProfileRequest(
            "  Felipe ",
            " Customer  ",
            "+5511999990000",
            "br",
            profile.Version);
        var updateResponse = await client.PutAsJsonAsync(
            "/api/customers/me",
            update);
        updateResponse.EnsureSuccessStatusCode();

        var updated =
            (await updateResponse.Content.ReadFromJsonAsync<CustomerProfileDto>())!;
        Assert.Equal("Felipe", updated.FirstName);
        Assert.Equal("Customer", updated.LastName);
        Assert.Equal("+5511999990000", updated.PhoneNumber);
        Assert.Equal("BR", updated.PhoneRegionCode);

        var staleResponse = await client.PutAsJsonAsync(
            "/api/customers/me",
            update);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task OrderEndpoints_CreateSnapshotsClearCartAndEnforceOwnership()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(
            client,
            Token(data.UserId, "orders:read orders:write", "OrderUser"));

        var createResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var order =
            (await createResponse.Content.ReadFromJsonAsync<OrderDetailDto>())!;

        Assert.Equal("Pending", order.Status);
        Assert.Equal(data.Email, order.CustomerEmail);
        Assert.Equal("Home recipient", order.ShippingAddress.RecipientName);
        Assert.Equal("+5511999990000", order.ShippingAddress.PhoneNumber);
        Assert.Equal("BR", order.ShippingAddress.PhoneRegionCode);
        Assert.Equal(50m, order.GrandTotal);
        Assert.Equal(2, Assert.Single(order.Items).Quantity);

        var retryResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        var retriedOrder =
            (await retryResponse.Content.ReadFromJsonAsync<OrderDetailDto>())!;
        Assert.Equal(order.Id, retriedOrder.Id);

        await using (var cartDb = fixture.CreateDbContext())
        {
            var cart = await cartDb.Carts
                .Include(item => item.Items)
                .SingleAsync(item => item.UserId == data.UserId.ToString());
            Assert.Empty(cart.Items);
            Assert.NotEqual(data.CartVersion, cart.Version);
        }

        var list = await client.GetFromJsonAsync<PagedOrdersDto>(
            "/api/orders?page=1&pageSize=20");
        Assert.Single(list!.Items);
        Assert.Equal(order.Id, list.Items[0].Id);

        var detail = await client.GetFromJsonAsync<OrderDetailDto>(
            $"/api/orders/{order.Id}");
        Assert.Equal(order.Id, detail!.Id);

        Authenticate(
            client,
            Token(data.OtherUserId, "orders:read", "OrderUser"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/orders/{order.Id}")).StatusCode);
    }

    [Fact]
    public async Task StripeCheckout_ReusesSessionChecksOwnershipAndProcessesPaymentOnce()
    {
        if (!fixture.IsEnabled) return;
        var gateway = new FakeStripeGateway
        {
            FailAfterCreateOnce = true
        };
        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStripeCheckoutGateway>();
                services.AddSingleton<IStripeCheckoutGateway>(gateway);
                services.Configure<StripeOptions>(options =>
                {
                    options.ApiKey = "sk_test_fixture";
                });
            }));
        using var client = configuredFactory.CreateClient();
        Authenticate(client, Token(data.UserId, "orders:read orders:write", "OrderUser"));
        var created = await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        created.EnsureSuccessStatusCode();
        var order = (await created.Content.ReadFromJsonAsync<OrderDetailDto>())!;
        var path = $"/api/orders/{order.Id}/checkout";
        var ambiguousResponse = await client.PostAsync(path, null);
        Assert.Equal(
            HttpStatusCode.BadGateway,
            ambiguousResponse.StatusCode);

        var response = await client.PostAsync(path, null);
        response.EnsureSuccessStatusCode();
        var session =
            (await response.Content.ReadFromJsonAsync<CheckoutSessionDto>())!;

        (await client.PostAsync(path, null)).EnsureSuccessStatusCode();

        PaymentAttempt checkoutAttempt;
        await using (var attemptDb = fixture.CreateDbContext())
        {
            checkoutAttempt = await attemptDb.PaymentAttempts
                .AsNoTracking()
                .SingleAsync(attempt => attempt.Order.PublicId == order.Id);
        }

        Assert.Equal(2, gateway.CreateCount);
        Assert.Equal(5000, gateway.Current.AmountTotal);
        Assert.Equal(
            $"stripe-checkout-{checkoutAttempt.PublicId:D}",
            Assert.Single(gateway.IdempotencyKeys.Distinct()));
        Assert.Equal(
            checkoutAttempt.PublicId.ToString("D"),
            gateway.Current.Metadata["payment_attempt_id"]);
        Assert.Equal(
            PaymentAttemptStatus.CheckoutCreated,
            checkoutAttempt.Status);
        Assert.Equal(
            session.SessionId,
            checkoutAttempt.ProviderSessionId);
        Assert.Equal("cs_test_fixture_secret_fixture", session.ClientSecret);
        Assert.Equal("embedded_page", gateway.LastRequest!.UiMode);
        Assert.Equal("never", gateway.LastRequest.RedirectOnCompletion);
        Assert.Equal(
            "card",
            Assert.Single(gateway.LastRequest.AllowedPaymentMethodTypes));
        Assert.Null(gateway.LastRequest.ReturnUrl);
        Assert.Null(gateway.LastRequest.SuccessUrl);
        Assert.Null(gateway.LastRequest.CancelUrl);

        await using (var reservationDb = fixture.CreateDbContext())
        {
            var inventory = await reservationDb.ProductInventories.SingleAsync();
            Assert.Equal(10, inventory.OnHand);
            var reservation = await reservationDb.InventoryReservations.SingleAsync();
            Assert.Equal(2, reservation.Quantity);
            Assert.Equal(reservation.ExpiresAtUtc, gateway.LastRequest.ExpiresAt);
            Assert.InRange(
                reservation.ExpiresAtUtc - reservation.CreatedAtUtc,
                TimeSpan.FromMinutes(29),
                TimeSpan.FromMinutes(31));
        }

        Authenticate(client, Token(data.OtherUserId, "orders:write", "OrderUser"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(path, null)).StatusCode);

        gateway.Current.Status = "complete";
        gateway.Current.PaymentStatus = "paid";
        var evt = new Stripe.Event
        {
            Id = "evt_fixture",
            Type = "checkout.session.completed",
            Data = new Stripe.EventData { Object = gateway.Current }
        };
        // Wrong amount must never confirm the order.
        gateway.Current.AmountTotal = 1;
        await using (var scope = configuredFactory.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<IStripeWebhookProcessor>()
                .ProcessAsync(evt, default));
        gateway.Current.AmountTotal = 5000;
        for (var i = 0; i < 2; i++)
        {
            await using var scope = configuredFactory.Services.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IStripeWebhookProcessor>()
                .ProcessAsync(evt, default));
        }

        Authenticate(client, Token(data.UserId, "orders:read orders:write", "OrderUser"));
        var paid = (await client.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{order.Id}"))!;
        Assert.Equal("Paid", paid.PaymentStatus);
        Assert.Equal("Confirmed", paid.Status);
        Assert.NotNull(paid.PaidAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path, null)).StatusCode);
        await using var db = fixture.CreateDbContext();
        var storedOrder = await db.Orders.SingleAsync(
            stored => stored.PublicId == order.Id);
        var storedAttempt = await db.PaymentAttempts.SingleAsync(
            stored => stored.PublicId == checkoutAttempt.PublicId);
        Assert.Equal(paid.PaidAtUtc, storedOrder.PaidAtUtc);
        Assert.Equal(PaymentAttemptStatus.Paid, storedAttempt.Status);
        Assert.Equal(session.SessionId, storedAttempt.ProviderSessionId);
        Assert.NotNull(storedAttempt.CompletedAtUtc);
        var consumedInventory = await db.ProductInventories.SingleAsync();
        Assert.Equal(8, consumedInventory.OnHand);
        Assert.Empty(await db.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task StripeExpiration_ReleasesReservedStock()
    {
        if (!fixture.IsEnabled) return;

        var gateway = new FakeStripeGateway();
        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStripeCheckoutGateway>();
                services.AddSingleton<IStripeCheckoutGateway>(gateway);
                services.Configure<StripeOptions>(options =>
                    options.ApiKey = "sk_test_fixture");
            }));
        using var client = configuredFactory.CreateClient();
        Authenticate(client, Token(data.UserId, "orders:write", "OrderUser"));
        var created = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        created.EnsureSuccessStatusCode();
        var order = (await created.Content.ReadFromJsonAsync<OrderDetailDto>())!;
        (await client.PostAsync($"/api/orders/{order.Id}/checkout", null))
            .EnsureSuccessStatusCode();

        gateway.Current.Status = "expired";
        gateway.Current.PaymentStatus = "unpaid";
        var evt = new Stripe.Event
        {
            Id = "evt_expired_fixture",
            Type = "checkout.session.expired",
            Data = new Stripe.EventData { Object = gateway.Current }
        };
        await using (var scope = configuredFactory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider
                .GetRequiredService<IStripeWebhookProcessor>()
                .ProcessAsync(evt, default));
        }

        await using var db = fixture.CreateDbContext();
        var inventory = await db.ProductInventories.SingleAsync();
        Assert.Equal(10, inventory.OnHand);
        Assert.Empty(await db.InventoryReservations.ToListAsync());
        var storedOrder = await db.Orders.SingleAsync(
            item => item.PublicId == order.Id);
        Assert.Equal(OrderPaymentStatus.Expired, storedOrder.PaymentStatus);
    }

    [Fact]
    public async Task ExpiredReservation_NoLongerReducesAvailableStockWithoutWebhook()
    {
        if (!fixture.IsEnabled) return;

        var gateway = new FakeStripeGateway();
        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStripeCheckoutGateway>();
                services.AddSingleton<IStripeCheckoutGateway>(gateway);
                services.Configure<StripeOptions>(options =>
                    options.ApiKey = "sk_test_fixture");
            }));
        using var client = configuredFactory.CreateClient();
        Authenticate(client, Token(data.UserId, "orders:write", "OrderUser"));
        var created = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        created.EnsureSuccessStatusCode();
        var order = (await created.Content.ReadFromJsonAsync<OrderDetailDto>())!;
        (await client.PostAsync($"/api/orders/{order.Id}/checkout", null))
            .EnsureSuccessStatusCode();

        Authenticate(client, Token(data.UserId, "", "ProductReader"));
        var reserved = await client.GetFromJsonAsync<ProductDto>(
            $"/api/products/{data.ProductId}");
        Assert.Equal(8, reserved!.AvailableStock);

        await using (var db = fixture.CreateDbContext())
        {
            var reservation = await db.InventoryReservations.SingleAsync();
            reservation.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var expired = await client.GetFromJsonAsync<ProductDto>(
            $"/api/products/{data.ProductId}");
        Assert.Equal(10, expired!.AvailableStock);
        Assert.True(expired.IsInStock);
        await using (var scope = configuredFactory.Services.CreateAsyncScope())
        {
            var refreshedCachedProduct = await scope.ServiceProvider
                .GetRequiredService<ProductAvailabilityService>()
                .RefreshAsync(reserved, default);
            Assert.Equal(10, refreshedCachedProduct.AvailableStock);
        }
        await using var verificationDb = fixture.CreateDbContext();
        Assert.Single(await verificationDb.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentReservations_CannotClaimTheSameFinalUnit()
    {
        if (!fixture.IsEnabled) return;

        long firstOrderId;
        long secondOrderId;
        await using (var setupDb = fixture.CreateDbContext())
        {
            var product = await setupDb.Products
                .Include(item => item.Inventory)
                .SingleAsync();
            product.Inventory!.OnHand = 1;

            var customer = await setupDb.Customers.SingleAsync(
                item => item.UserId == data.UserId);
            var first = CreateReservationTestOrder(customer.Id, product.Id);
            var second = CreateReservationTestOrder(customer.Id, product.Id);
            setupDb.Orders.AddRange(first, second);
            await setupDb.SaveChangesAsync();
            firstOrderId = first.Id;
            secondOrderId = second.Id;
        }

        async Task<InventoryReservationResult> ReserveAsync(long orderId)
        {
            await using var db = fixture.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var order = await db.Orders
                .Include(item => item.Items)
                .SingleAsync(item => item.Id == orderId);
            var service = new InventoryReservationService(
                db,
                Options.Create(new InventoryOptions
                {
                    ReservationMinutes = 30
                }),
                TimeProvider.System);
            var result = await service.EnsureReservedAsync(
                order,
                allowExpiredReplacement: true,
                default);

            if (result.IsSuccess)
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            return result;
        }

        var results = await Task.WhenAll(
            ReserveAsync(firstOrderId),
            ReserveAsync(secondOrderId));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => !result.IsSuccess);
        await using var verificationDb = fixture.CreateDbContext();
        var reservation = await verificationDb.InventoryReservations
            .SingleAsync();
        Assert.Equal(1, reservation.Quantity);
        Assert.True(reservation.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task StripeCheckout_RejectsOrderWhenStockChangedBeforeReservation()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Configure<StripeOptions>(options =>
                    options.ApiKey = "sk_test_fixture")));
        using var client = configuredFactory.CreateClient();
        Authenticate(client, Token(data.UserId, "orders:write", "OrderUser"));
        var created = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        created.EnsureSuccessStatusCode();
        var order = (await created.Content.ReadFromJsonAsync<OrderDetailDto>())!;

        await using (var stockDb = fixture.CreateDbContext())
        {
            var inventory = await stockDb.ProductInventories.SingleAsync();
            inventory.OnHand = 1;
            await stockDb.SaveChangesAsync();
        }

        var checkout = await client.PostAsync(
            $"/api/orders/{order.Id}/checkout",
            null);

        Assert.Equal(HttpStatusCode.Conflict, checkout.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Empty(await db.InventoryReservations.ToListAsync());
        Assert.Empty(await db.PaymentAttempts.ToListAsync());
        Assert.Equal(1, (await db.ProductInventories.SingleAsync()).OnHand);
    }

    private sealed class FakeStripeGateway : IStripeCheckoutGateway
    {
        public int CreateCount { get; private set; }
        public bool FailAfterCreateOnce { get; set; }
        public List<string> IdempotencyKeys { get; } = [];
        public Session Current { get; } = new()
        {
            Id = "cs_test_fixture",
            Status = "open",
            PaymentStatus = "unpaid",
            Mode = "payment",
            ClientSecret = "cs_test_fixture_secret_fixture"
        };
        public SessionCreateOptions? LastRequest { get; private set; }

        public Task<Session> GetAsync(string id, CancellationToken ct) => Task.FromResult(Current);
        public Task<Session> CreateAsync(SessionCreateOptions request, string idempotencyKey, CancellationToken ct)
        {
            CreateCount++;
            LastRequest = request;
            IdempotencyKeys.Add(idempotencyKey);
            Current.ClientReferenceId = request.ClientReferenceId;
            Current.Metadata = request.Metadata;
            Current.AmountTotal = request.LineItems.Single().PriceData.UnitAmount;
            Current.Currency = request.LineItems.Single().PriceData.Currency;
            if (FailAfterCreateOnce)
            {
                FailAfterCreateOnce = false;
                throw new Stripe.StripeException(
                    "Simulated lost Stripe response.");
            }

            return Task.FromResult(Current);
        }
    }
    private async Task<TestData> CreateTestDataAsync()
    {
        var userId = await CreateCustomerAsync();
        var otherUserId = await CreateCustomerAsync();
        var email = $"account-{userId:N}@example.com";
        var cartVersion = Guid.NewGuid();
        await using var db = fixture.CreateDbContext();

        var user = await db.Users.SingleAsync(item => item.Id == userId);
        user.Email = email;
        user.NormalizedEmail = email.ToUpperInvariant();

        var customer = await db.Customers.SingleAsync(
            item => item.UserId == userId);
        customer.PhoneNumberE164 = "+5511999990000";
        customer.PhoneRegionCode = "BR";
        var address = new CustomerAddress
        {
            CustomerId = customer.Id,
            Label = "Home",
            RecipientName = "Home recipient",
            PhoneNumber = "+55 11 99999-0000",
            AddressLine1 = "100 Main Street",
            City = "Sao Paulo",
            Region = "SP",
            PostalCode = "01000-000",
            CountryCode = "BR",
            IsDefault = true
        };
        var category = new Category { Name = $"Orders {Guid.NewGuid():N}" };
        var product = new Product
        {
            Name = "Order product",
            Category = category,
            ExternalProductId = $"order-{Guid.NewGuid():N}",
            Inventory = new ProductInventory { OnHand = 10 },
            Prices =
            [
                new ProductPrice
                {
                    ActualPrice = 25m,
                    CurrencyCode = "BRL",
                    CapturedAt = DateTime.UtcNow
                }
            ]
        };

        db.CustomerAddresses.Add(address);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.Carts.Add(new Cart
        {
            UserId = userId.ToString(),
            Version = cartVersion,
            Items =
            [
                new CartItem
                {
                    UserId = userId.ToString(),
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPriceAtAddition = 25m,
                    CurrencyAtAddition = "BRL",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            ]
        });
        await db.SaveChangesAsync();

        return new TestData(
            userId,
            otherUserId,
            email,
            address.PublicId,
            cartVersion,
            product.Id);
    }

    private static Order CreateReservationTestOrder(
        long customerId,
        long productId)
    {
        var utcNow = DateTime.UtcNow;
        return new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            PaymentStatus = OrderPaymentStatus.Pending,
            CustomerEmail = $"reservation-{Guid.NewGuid():N}@example.com",
            RecipientName = "Reservation test",
            ShippingAddressLine1 = "100 Main Street",
            ShippingCity = "Sao Paulo",
            ShippingRegion = "SP",
            ShippingPostalCode = "01000-000",
            ShippingCountryCode = "BR",
            CurrencyCode = "BRL",
            Subtotal = 25m,
            GrandTotal = 25m,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            Items =
            [
                new OrderItem
                {
                    ProductId = productId,
                    ProductName = "Order product",
                    Quantity = 1,
                    UnitPrice = 25m,
                    LineTotal = 25m
                }
            ]
        };
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        var userId = Guid.NewGuid();
        var email = $"account-{userId:N}@example.com";
        await using var db = fixture.CreateDbContext();

        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();

        db.Customers.Add(new Customer { UserId = userId });
        await db.SaveChangesAsync();

        return userId;
    }

    private static string Token(Guid userId, string scope, string role) =>
        TestECommerceJwt.CreateToken(
            userId.ToString(),
            scope,
            role: role);

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private sealed record TestData(
        Guid UserId,
        Guid OtherUserId,
        string Email,
        Guid AddressId,
        Guid CartVersion,
        long ProductId);

    private sealed class AccountFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureHostConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Database:MigrateOnStartup"] = "false",
                    ["Jwt:Issuer"] = "account-tests",
                    ["Jwt:Audience"] = "account-tests",
                    ["Jwt:Key"] = "account-test-signing-key-with-more-than-32-characters",
                    ["Jwt:ApiKey"] = "account-test-api-key",
                    ["Jwt:RoleClaimType"] = "role",
                    ["Jwt:ExpireMinutes"] = "30",
                    ["ECommerceJwt:Issuer"] = TestECommerceJwt.Issuer,
                    ["ECommerceJwt:Audience"] = TestECommerceJwt.Audience,
                    ["ECommerceJwt:PublicKey"] = TestECommerceJwt.PublicKey,
                    ["ECommerceJwt:KeyId"] = TestECommerceJwt.KeyId,
                    ["Redis:Enabled"] = "false"
                }));

            return base.CreateHost(builder);
        }
    }
}
