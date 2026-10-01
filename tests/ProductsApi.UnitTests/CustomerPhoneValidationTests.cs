using System.ComponentModel.DataAnnotations;
using ProductsApi.Features.Customers.Shared;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class CustomerPhoneValidationTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("+5511999990000", "BR", true)]
    [InlineData("+5511999990000", null, false)]
    [InlineData(null, "BR", false)]
    public void PhoneAndRegion_AreRequiredTogether(
        string? phoneNumber,
        string? phoneRegionCode,
        bool isValid)
    {
        var request = new UpdateCustomerProfileRequest(
            null,
            null,
            phoneNumber,
            phoneRegionCode,
            new byte[8]);

        var validationResults = request
            .Validate(new ValidationContext(request))
            .ToArray();

        Assert.Equal(isValid, validationResults.Length == 0);
    }

    [Theory]
    [InlineData("+5511999990000", true)]
    [InlineData("+12025550123", true)]
    [InlineData("+55 11 99999-0000", false)]
    [InlineData("5511999990000", false)]
    [InlineData("+0123456789", false)]
    public void PhoneNumber_RequiresE164Format(string phoneNumber, bool isValid)
    {
        var parameter = typeof(UpdateCustomerProfileRequest)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(item => item.Name == "PhoneNumber");
        var attribute = Assert.Single(
            parameter.GetCustomAttributes(typeof(RegularExpressionAttribute), true)
                .Cast<RegularExpressionAttribute>());

        Assert.Equal(isValid, attribute.IsValid(phoneNumber));
    }
}
