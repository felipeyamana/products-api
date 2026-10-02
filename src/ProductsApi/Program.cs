using Azure.Monitor.OpenTelemetry.AspNetCore;
using ProductsApi;
using ProductsApi.Common;
using ProductsApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrWhiteSpace(
        builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services
        .AddOpenTelemetry()
        .UseAzureMonitor();
}

builder.Services.AddProductsApi(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services
    .AddControllers()
    // [ApiController] rejects invalid requests before the action runs, so log these 400s here.
    .ConfigureApiBehaviorOptions(options =>
    {
        // Wrap the framework factory to preserve its standard ValidationProblemDetails response.
        var defaultFactory = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            // Record field names and counts only; values and error messages may contain sensitive data.
            // "$" represents a body-level validation or JSON-binding failure without a field name.
            var invalidFields = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry => string.IsNullOrWhiteSpace(entry.Key) ? "$" : entry.Key)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray();
            var errorCount = context.ModelState.Values.Sum(value => value.Errors.Count);
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("ProductsApi.Validation");

            logger.LogWarning(
                "Request validation failed for {RequestMethod} {RequestPath}. " +
                "Invalid fields: {InvalidFields}; error count: {ValidationErrorCount}; " +
                "trace identifier: {TraceIdentifier}.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path,
                string.Join(",", invalidFields),
                errorCount,
                context.HttpContext.TraceIdentifier);

            // Logging adds diagnostics without changing the response clients already receive.
            return defaultFactory(context);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Products API v1");
    });
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
