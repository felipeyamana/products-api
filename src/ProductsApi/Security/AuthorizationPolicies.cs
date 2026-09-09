namespace ProductsApi.Security;

public static class AuthorizationPolicies
{
    public const string CartRead = "Cart.Read";
    public const string CartWrite = "Cart.Write";
    public const string ProductsRead = "Products.Read";
    public const string ProductsWrite = "Products.Write";
}
