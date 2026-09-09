using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProductsApi.Data;

namespace ProductsApi.Features.Cart;

public sealed class CartLockManager(AppDbContext db)
{
    private const string ResourcePrefix = "cart:";
    private const int LockTimeoutMilliseconds = 10_000;

    public async Task<IDbContextTransaction> AcquireAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var resource = CreateCartResource(userId);

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;

                EXEC @result = sys.sp_getapplock
                    @Resource = {resource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = {LockTimeoutMilliseconds};

                IF @result < 0
                    THROW 51000, 'Could not acquire cart lock.', 1;
                """,
                cancellationToken);

            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static string CreateCartResource(string userId)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(userId)));

        return ResourcePrefix + hash;
    }
}
