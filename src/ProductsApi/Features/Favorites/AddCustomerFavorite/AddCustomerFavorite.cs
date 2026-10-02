using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Favorites.Shared;

namespace ProductsApi.Features.Favorites.AddCustomerFavorite;

public sealed record AddCustomerFavoriteCommand(Guid UserId, long ProductId);

public sealed class AddCustomerFavoriteHandler(AppDbContext dbContext)
    : ICommandHandler<AddCustomerFavoriteCommand, FavoriteResult<AddCustomerFavoriteResult>>
{
    public const int MaxFavorites = 500;

    public async Task<FavoriteResult<AddCustomerFavoriteResult>> Handle(
        AddCustomerFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ProductId <= 0)
        {
            return FavoriteResult<AddCustomerFavoriteResult>
                .BadRequest("Product ID must be positive.");
        }

        var customerId = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.UserId == command.UserId)
            .Select(customer => (long?)customer.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return FavoriteResult<AddCustomerFavoriteResult>
                .NotFound("Customer record not found.");
        }

        var existing = await FavoriteMapper.FindAsync(
            dbContext,
            customerId.Value,
            command.ProductId,
            cancellationToken);
        if (existing is not null)
        {
            return FavoriteResult<AddCustomerFavoriteResult>.Success(
                new AddCustomerFavoriteResult(existing, false));
        }

        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == command.ProductId, cancellationToken);
        if (!productExists)
        {
            return FavoriteResult<AddCustomerFavoriteResult>
                .NotFound("Product not found.");
        }

        var favoriteCount = await dbContext.CustomerFavorites
            .CountAsync(favorite => favorite.CustomerId == customerId.Value, cancellationToken);
        if (favoriteCount >= MaxFavorites)
        {
            return FavoriteResult<AddCustomerFavoriteResult>
                .Conflict($"A customer cannot save more than {MaxFavorites} favorites.");
        }

        var favorite = new CustomerFavorite
        {
            CustomerId = customerId.Value,
            ProductId = command.ProductId,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.CustomerFavorites.Add(favorite);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var concurrentlyAdded = await FavoriteMapper.FindAsync(
                dbContext,
                customerId.Value,
                command.ProductId,
                cancellationToken);
            if (concurrentlyAdded is not null)
            {
                return FavoriteResult<AddCustomerFavoriteResult>.Success(
                    new AddCustomerFavoriteResult(concurrentlyAdded, false));
            }

            throw;
        }

        var created = await FavoriteMapper.FindAsync(
            dbContext,
            customerId.Value,
            command.ProductId,
            cancellationToken);

        return FavoriteResult<AddCustomerFavoriteResult>.Success(
            new AddCustomerFavoriteResult(created!, true));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
