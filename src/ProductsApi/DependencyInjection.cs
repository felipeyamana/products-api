using ProductsApi.Common.Cqrs;
using Microsoft.EntityFrameworkCore;
using ProductsApi.Data;
using ProductsApi.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Threading.RateLimiting;

namespace ProductsApi;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not configured. " +
                "Set ConnectionStrings__DefaultConnection in the environment.");
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "JWT settings were not configured. Set Jwt__Issuer, Jwt__Audience, Jwt__Key, and Jwt__ApiKey in the environment.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
            string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
            string.IsNullOrWhiteSpace(jwtOptions.Key) ||
            string.IsNullOrWhiteSpace(jwtOptions.ApiKey))
        {
            throw new InvalidOperationException(
                "JWT settings are incomplete. Set Jwt__Issuer, Jwt__Audience, Jwt__Key, and Jwt__ApiKey in the environment.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure()));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    RoleClaimType = jwtOptions.RoleClaimType
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.ProductsRead, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(AuthorizationPolicies.ProductsWrite, policy =>
                policy.RequireRole("Admin", "ProductManager"));
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many requests. Please try again later." },
                    cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetIpPartitionKey(httpContext, "auth"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicies.Products, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetUserPartitionKey(httpContext, "products"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        services.Scan(scan => scan
            .FromAssemblies(Assembly.GetExecutingAssembly())
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        return services;
    }

    private static string GetIpPartitionKey(HttpContext httpContext, string policyName)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{policyName}:ip:{remoteIp}";
    }

    private static string GetUserPartitionKey(HttpContext httpContext, string policyName)
    {
        var subject = httpContext.User.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(subject)
            ? GetIpPartitionKey(httpContext, policyName)
            : $"{policyName}:sub:{subject}";
    }
}
