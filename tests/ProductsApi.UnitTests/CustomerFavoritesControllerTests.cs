using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductsApi.Common.Cqrs;
using ProductsApi.Controllers;
using ProductsApi.Features.Favorites.AddCustomerFavorite;
using ProductsApi.Features.Favorites.GetCustomerFavorites;
using ProductsApi.Features.Favorites.RemoveCustomerFavorite;
using ProductsApi.Features.Favorites.Shared;
using ProductsApi.Features.Products.Shared;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class CustomerFavoritesControllerTests
{
    [Fact]
    public async Task AddFavorite_WhenCreated_ReturnsCreatedAndDispatchesAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var favorite = CreateFavorite(42);
        var commandDispatcher = new Mock<ICommandDispatcher>();
        commandDispatcher
            .Setup(dispatcher => dispatcher.Dispatch<
                AddCustomerFavoriteCommand,
                FavoriteResult<AddCustomerFavoriteResult>>(
                    It.Is<AddCustomerFavoriteCommand>(command =>
                        command.UserId == userId && command.ProductId == favorite.Product.Id),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(FavoriteResult<AddCustomerFavoriteResult>.Success(
                new AddCustomerFavoriteResult(favorite, true)));
        var controller = CreateController(userId, commandDispatcher: commandDispatcher);

        var response = await controller.AddFavorite(
            favorite.Product.Id,
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response);
        Assert.Equal(nameof(CustomerFavoritesController.GetFavorites), created.ActionName);
        Assert.Same(favorite, created.Value);
    }

    [Fact]
    public async Task GetFavorites_WhenCustomerIsMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var queryDispatcher = new Mock<IQueryDispatcher>();
        queryDispatcher
            .Setup(dispatcher => dispatcher.Dispatch<
                GetCustomerFavoritesQuery,
                FavoriteResult<IReadOnlyList<CustomerFavoriteDto>>>(
                    It.Is<GetCustomerFavoritesQuery>(query => query.UserId == userId),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(FavoriteResult<IReadOnlyList<CustomerFavoriteDto>>
                .NotFound("Customer record not found."));
        var controller = CreateController(userId, queryDispatcher: queryDispatcher);

        var response = await controller.GetFavorites(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response);
    }

    [Fact]
    public async Task RemoveFavorite_WhenSuccessful_ReturnsNoContent()
    {
        var userId = Guid.NewGuid();
        var commandDispatcher = new Mock<ICommandDispatcher>();
        commandDispatcher
            .Setup(dispatcher => dispatcher.Dispatch<
                RemoveCustomerFavoriteCommand,
                FavoriteResult<bool>>(
                    It.Is<RemoveCustomerFavoriteCommand>(command =>
                        command.UserId == userId && command.ProductId == 42),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(FavoriteResult<bool>.Success(true));
        var controller = CreateController(userId, commandDispatcher: commandDispatcher);

        var response = await controller.RemoveFavorite(42, CancellationToken.None);

        Assert.IsType<NoContentResult>(response);
    }

    private static CustomerFavoritesController CreateController(
        Guid userId,
        Mock<IQueryDispatcher>? queryDispatcher = null,
        Mock<ICommandDispatcher>? commandDispatcher = null)
    {
        var controller = new CustomerFavoritesController(
            (queryDispatcher ?? new Mock<IQueryDispatcher>()).Object,
            (commandDispatcher ?? new Mock<ICommandDispatcher>()).Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim("sub", userId.ToString())]))
            }
        };
        return controller;
    }

    private static CustomerFavoriteDto CreateFavorite(long productId) =>
        new(
            DateTime.UtcNow,
            new ProductDto(
                productId,
                "Favorite product",
                "Acme",
                "Description",
                1,
                "Category",
                null,
                null,
                $"external-{productId}",
                4.5m,
                10,
                true,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow,
                49.99m,
                59.99m,
                "USD"));
}
