using Microsoft.EntityFrameworkCore;
using ProductsApi.Data.Entities;
using Xunit;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class DatabaseResetTests(MsSqlContainerFixture fixture)
{
    [Fact]
    public async Task ResetDatabase_RemovesPaymentAttemptsBeforeTheirOrders()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await fixture.ResetDatabaseAsync();

        await using (var db = fixture.CreateDbContext())
        {
            db.PaymentAttempts.Add(new PaymentAttempt
            {
                PublicId = Guid.NewGuid(),
                AttemptNumber = 1,
                Provider = "Stripe",
                IdempotencyKey = Guid.NewGuid().ToString(),
                Amount = 25m,
                CurrencyCode = "BRL",
                Order = new Order
                {
                    Customer = new Customer(),
                    CustomerEmail = "reset@example.com",
                    RecipientName = "Test customer",
                    ShippingAddressLine1 = "100 Main Street",
                    ShippingCity = "Sao Paulo",
                    ShippingRegion = "SP",
                    ShippingPostalCode = "01000-000",
                    ShippingCountryCode = "BR",
                    CurrencyCode = "BRL",
                    Subtotal = 25m,
                    GrandTotal = 25m
                }
            });
            await db.SaveChangesAsync();
            Assert.True(await db.PaymentAttempts.AnyAsync());
        }

        await fixture.ResetDatabaseAsync();

        await using var verificationDb = fixture.CreateDbContext();
        Assert.False(await verificationDb.PaymentAttempts.AnyAsync());
        Assert.False(await verificationDb.Orders.AnyAsync());
        Assert.False(await verificationDb.Customers.AnyAsync());

        await fixture.ResetDatabaseAsync();
    }
}
