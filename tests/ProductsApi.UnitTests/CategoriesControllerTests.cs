using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductsApi.Common.Cqrs;
using ProductsApi.Controllers;
using ProductsApi.Features.Categories.GetCategories;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class CategoriesControllerTests
{
    [Fact]
    public async Task GetCategories_ReturnsDispatchedCategories()
    {
        IReadOnlyList<CategoryDto> categories =
        [
            new CategoryDto(1, "Books", null),
            new CategoryDto(2, "Fiction", 1)
        ];
        var queryDispatcher = new Mock<IQueryDispatcher>();
        queryDispatcher
            .Setup(dispatcher => dispatcher.Dispatch<GetCategoriesQuery, IReadOnlyList<CategoryDto>>(
                It.IsAny<GetCategoriesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);
        var controller = new CategoriesController(queryDispatcher.Object);

        var response = await controller.GetCategories(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(categories, okResult.Value);
    }

    [Fact]
    public void GetCategories_RequiresProductReadAuthorization()
    {
        var method = typeof(CategoriesController).GetMethod(nameof(CategoriesController.GetCategories));

        Assert.NotNull(method);
        var authorize = Assert.Single(
            method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
        Assert.Equal(ProductsApi.Security.AuthorizationPolicies.ProductsRead, authorize.Policy);
    }
}
