using System.ComponentModel.DataAnnotations;
using ProductsApi.Features.CustomerAddresses.Shared;
using ProductsApi.Features.Customers.Shared;
using ProductsApi.Features.Orders.Shared;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class RequestValidationMetadataTests
{
    public static TheoryData<Type> PositionalRequestRecordTypes =>
    [
        typeof(CreateCustomerAddressRequest),
        typeof(UpdateCustomerAddressRequest),
        typeof(SetDefaultCustomerAddressRequest),
        typeof(UpdateCustomerProfileRequest),
        typeof(CreateOrderRequest)
    ];

    [Theory]
    [MemberData(nameof(PositionalRequestRecordTypes))]
    public void ValidationAttributes_AreDeclaredOnPrimaryConstructorParameters(Type requestType)
    {
        var propertyValidationAttributes = requestType
            .GetProperties()
            .SelectMany(property => property.GetCustomAttributes(typeof(ValidationAttribute), inherit: true));

        Assert.Empty(propertyValidationAttributes);

        var constructorParameters = Assert.Single(requestType.GetConstructors()).GetParameters();
        Assert.Contains(
            constructorParameters,
            parameter => parameter.GetCustomAttributes(typeof(ValidationAttribute), inherit: true).Length > 0);
    }
}