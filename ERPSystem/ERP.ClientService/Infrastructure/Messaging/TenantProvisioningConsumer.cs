using Confluent.Kafka;
using ERP.ClientService.Domain;
using ERP.ClientService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ERP.ClientService.Infrastructure.Messaging;

public class TenantProvisioningConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<TenantProvisioningConsumer> _logger;

    public TenantProvisioningConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<TenantProvisioningConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"],
            GroupId = "client-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        consumer.Subscribe("tenant.activated");

        _logger.LogInformation("ClientService listening on topic 'tenant.activated'...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var evt = JsonSerializer.Deserialize<TenantActivatedEvent>(result.Message.Value);

                if (evt is null)
                {
                    consumer.Commit(result);
                    continue;
                }

                _logger.LogInformation(
                    "Received TenantActivatedEvent for tenant '{Slug}'", evt.SubdomainSlug);

                await ProvisionTenantDatabaseAsync(evt, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing TenantActivatedEvent");
            }
        }

        consumer.Close();
    }

    private async Task ProvisionTenantDatabaseAsync(TenantActivatedEvent evt, CancellationToken ct)
    {
        var baseConnection = _config.GetConnectionString("DefaultConnection")!;
        var dbName = $"ERPClientsDb_{evt.SubdomainSlug}";
        var tenantConnection = baseConnection.Replace("ERPClientsDb", dbName);

        _logger.LogInformation("Provisioning database for tenant '{Slug}'...", evt.SubdomainSlug);

        // Step 1: Create DB and apply migrations
        var optionsBuilder = new DbContextOptionsBuilder<ClientDbContext>();
        optionsBuilder.UseSqlServer(tenantConnection);
        await using var context = new ClientDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync(ct);

        // Step 2: Grant ownership from master using raw ADO.NET
        var masterConnection = baseConnection.Replace("Database=ERPClientsDb", "Database=master");
        var currentUser = $"{Environment.UserDomainName}\\{Environment.UserName}";

        try
        {
            await using var conn = new SqlConnection(masterConnection);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER AUTHORIZATION ON DATABASE::[{dbName}] TO [{currentUser}]";
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Ownership granted on {DbName} to {User}", dbName, currentUser);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not alter authorization on {DbName}, continuing...", dbName);
        }

        // Step 3: Seed categories
        await SeedCategoriesAsync(context, evt.SubdomainSlug);

        _logger.LogInformation("Database provisioned for tenant '{Slug}'", evt.SubdomainSlug);
    }

    private async Task SeedCategoriesAsync(ClientDbContext context, string slug)
    {
        bool exists = await context.Categories.AnyAsync();
        if (exists)
        {
            _logger.LogInformation("Categories already seeded for tenant '{Slug}', skipping.", slug);
            return;
        }

        var categories = new[]
        {
            Category.Create("Standard",      "STD", 15, 30, false, null,   null),
            Category.Create("VIP",           "VIP", 60, 60, true,  0.10m, 1.5m),
            Category.Create("Wholesale",     "WHL", 30, 45, true,  0.15m, 2.0m),
            Category.Create("Public Sector", "PUB", 45, 60, false, null,  1.2m),
            Category.Create("Reseller",      "RSL", 30, 45, true,  0.20m, 1.8m),
            Category.Create("New Client",    "NEW",  7, 15, false, null,   null),
        };

        await context.Categories.AddRangeAsync(categories);

        var legacy = Category.Create("Legacy", "LGC", 10, 30, false, null, null);
        legacy.Deactivate();
        await context.Categories.AddAsync(legacy);

        await context.SaveChangesAsync();

        _logger.LogInformation("Categories seeded for tenant '{Slug}'", slug);
    }
}