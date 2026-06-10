using ProductsApi.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace ProductsApi.IntegrationTests;

public sealed class MsSqlContainerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public bool IsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("RUN_TESTCONTAINERS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public string ConnectionString =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("SQL Server test container is not running.");

    public async Task InitializeAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        _container = new MsSqlBuilder()
            .WithPassword("SqlServer_TestPassword123")
            .Build();

        await _container.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class MsSqlCollection : ICollectionFixture<MsSqlContainerFixture>
{
    public const string Name = "MsSql";
}
