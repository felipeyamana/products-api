using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProductsApi.Caching;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Cart;
using ProductsApi.Security;
using StackExchange.Redis;

namespace ProductsApi;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = GetJwtOptions(configuration);
        var ecommerceJwtOptions = GetECommerceJwtOptions(configuration);

        services.AddProductsData(configuration);
        services.AddProductsIdentity();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddProductsAuthentication(jwtOptions, ecommerceJwtOptions);
        services.AddProductsAuthorization();
        services.AddProductsRateLimiting(jwtOptions.ApiKey);
        services.AddProductFeatures();
        services.AddProductCaching(configuration);

        return services;
    }

    private static IServiceCollection AddProductsData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not configured. " +
                "Set ConnectionStrings__DefaultConnection in the environment.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure()));

        return services;
    }

    private static IServiceCollection AddProductsIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        return services;
    }

    private static IServiceCollection AddProductsAuthentication(
        this IServiceCollection services,
        JwtOptions jwtOptions,
        ECommerceJwtOptions ecommerceJwtOptions)
    {
        var ecommerceSigningKey = ecommerceJwtOptions.CreateSecurityKey();

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
            })
            .AddJwtBearer(ECommerceJwtOptions.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = ecommerceJwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = ecommerceJwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (_, _, keyId, _) =>
                        StringComparer.Ordinal.Equals(keyId, ecommerceJwtOptions.KeyId)
                            ? [ecommerceSigningKey]
                            : [],
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    RoleClaimType = "role"
                };
            });

        return services;
    }

    private static IServiceCollection AddProductsAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.CartRead, policy => policy
                .AddAuthenticationSchemes(ECommerceJwtOptions.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("CartUser")
                .RequireClaim("sub")
                .RequireAssertion(context => HasScope(context.User, "cart:read")));

            options.AddPolicy(AuthorizationPolicies.CartWrite, policy => policy
                .AddAuthenticationSchemes(ECommerceJwtOptions.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("CartUser")
                .RequireClaim("sub")
                .RequireAssertion(context => HasScope(context.User, "cart:write")));

            options.AddPolicy(AuthorizationPolicies.ProductsRead, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(AuthorizationPolicies.ProductsWrite, policy =>
                policy.RequireRole("Admin", "ProductManager"));
        });

        return services;
    }

    private static IServiceCollection AddProductsRateLimiting(
        this IServiceCollection services,
        string apiKey)
    {
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
                    GetAuthPartitionKey(httpContext, apiKey),
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

        return services;
    }

    private static IServiceCollection AddProductFeatures(this IServiceCollection services)
    {
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<CartLockManager>();
        services.AddScoped<CartService>();

        services.Scan(scan => scan
            .FromAssemblies(Assembly.GetExecutingAssembly())
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        return services;
    }

    private static IServiceCollection AddProductCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();

        if (redisOptions is null)
        {
            return services;
        }

        if (!redisOptions.Enabled || string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
        {
            if (redisOptions.RegisterNullCacheWhenDisabled)
            {
                services.AddSingleton<IProductCache, NullProductCache>();
            }

            return services;
        }

        ConfigurationOptions configurationOptions;
        try
        {
            configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            configurationOptions.AbortOnConnectFail = false;
            configurationOptions.ConnectTimeout = redisOptions.ConnectTimeoutMilliseconds;
            configurationOptions.SyncTimeout = redisOptions.SyncTimeoutMilliseconds;
            configurationOptions.AsyncTimeout = redisOptions.AsyncTimeoutMilliseconds;
        }
        catch
        {
            if (redisOptions.RegisterNullCacheWhenDisabled)
            {
                services.AddSingleton<IProductCache, NullProductCache>();
            }

            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = configurationOptions;
            options.InstanceName = string.IsNullOrWhiteSpace(redisOptions.InstanceName)
                ? "products-api:"
                : redisOptions.InstanceName;
        });
        services.AddSingleton<IProductCache, ProductCache>();

        return services;
    }

    private static JwtOptions GetJwtOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "JWT settings were not configured. Set Jwt__Issuer, Jwt__Audience, Jwt__Key, and Jwt__ApiKey in the environment.");

        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            string.IsNullOrWhiteSpace(options.Key) ||
            string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "JWT settings are incomplete. Set Jwt__Issuer, Jwt__Audience, Jwt__Key, and Jwt__ApiKey in the environment.");
        }

        return options;
    }

    private static ECommerceJwtOptions GetECommerceJwtOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(ECommerceJwtOptions.SectionName).Get<ECommerceJwtOptions>()
            ?? throw new InvalidOperationException(
                "ECommerce JWT settings were not configured. Set ECommerceJwt__Issuer, ECommerceJwt__Audience, ECommerceJwt__PublicKey, and ECommerceJwt__KeyId.");

        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            string.IsNullOrWhiteSpace(options.PublicKey) ||
            string.IsNullOrWhiteSpace(options.KeyId))
        {
            throw new InvalidOperationException(
                "ECommerce JWT settings are incomplete. Set ECommerceJwt__Issuer, ECommerceJwt__Audience, ECommerceJwt__PublicKey, and ECommerceJwt__KeyId.");
        }

        return options;
    }

    private static string GetIpPartitionKey(HttpContext httpContext, string policyName)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{policyName}:ip:{remoteIp}";
    }

    private static string GetAuthPartitionKey(HttpContext httpContext, string apiKey)
    {
        var hasTrustedCaller =
            httpContext.Request.Headers.TryGetValue("X-API-Key", out var suppliedApiKey) &&
            StringComparer.Ordinal.Equals(suppliedApiKey.ToString(), apiKey);
        var clientIp = httpContext.Request.Headers["X-Client-IP"].ToString();

        if (hasTrustedCaller &&
            !string.IsNullOrWhiteSpace(clientIp) &&
            clientIp.Length <= 64)
        {
            return $"auth:client-ip:{clientIp}";
        }

        return GetIpPartitionKey(httpContext, "auth");
    }

    private static string GetUserPartitionKey(HttpContext httpContext, string policyName)
    {
        var subject = httpContext.User.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(subject)
            ? GetIpPartitionKey(httpContext, policyName)
            : $"{policyName}:sub:{subject}";
    }

    private static bool HasScope(ClaimsPrincipal user, string requiredScope) =>
        user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
}
