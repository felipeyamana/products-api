namespace ProductsApi.Security;

public static class AuthorizationPolicies
{
    public const string CartRead = "Cart.Read";
    public const string CartWrite = "Cart.Write";
    public const string AddressesRead = "Addresses.Read";
    public const string AddressesWrite = "Addresses.Write";
    public const string CustomersRead = "Customers.Read";
    public const string CustomersWrite = "Customers.Write";
    public const string OrdersRead = "Orders.Read";
    public const string OrdersWrite = "Orders.Write";
    public const string ProductsRead = "Products.Read";
    public const string ProductsWrite = "Products.Write";
}
