namespace ProductsApi.Payments;

public static class PaymentServiceCollectionExtensions
{
    public static IServiceCollection AddStripePayments(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StripeOptions>(
            configuration.GetSection("Stripe"));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<OrderPaymentLock>();
        services.AddScoped<IStripeCheckoutGateway, StripeCheckoutGateway>();
        services.AddScoped<IStripeWebhookProcessor, StripeWebhookProcessor>();

        return services;
    }
}