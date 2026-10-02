namespace ProductsApi.Security;

public static class RateLimitPolicies
{
    public const string Auth = "Auth.Strict";

    public const string ServiceToken = "ServiceToken";

    public const string Products = "Products.Strict";
}
