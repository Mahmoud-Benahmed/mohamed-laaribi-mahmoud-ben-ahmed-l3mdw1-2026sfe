using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.ClientService.Infrastructure.Persistence.Seeders;

public class DatabaseSeeder
{
    private readonly CategorySeeder _categorySeeder;
    private readonly ClientSeeder _clientSeeder;
    private readonly ClientDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        CategorySeeder categorySeeder,
        ClientSeeder clientSeeder,
        ClientDbContext context,
        ILogger<DatabaseSeeder> logger)
    {
        _categorySeeder = categorySeeder;
        _clientSeeder = clientSeeder;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Applies pending migrations then runs seeders in dependency order:
    /// 1. Categories first — clients reference them
    /// 2. Clients second
    /// </summary>
    public async Task SeedAsync()
    {
        _logger.LogInformation("Applying pending migrations...");
        await _context.Database.MigrateAsync();

        _logger.LogInformation("Starting database seed...");

        List<Category> categories = await _categorySeeder.SeedAsync();

        await _clientSeeder.SeedAsync(categories);

        _logger.LogInformation("Database seed complete.");
    }
}

// ── Extension method for Program.cs ──────────────────────────────────────────

public static class DatabaseSeederExtensions
{
    /// <summary>
    /// Registers all seeders in DI and exposes a one-line call for Program.cs.
    /// </summary>
    public static IServiceCollection AddDatabaseSeeders(
        this IServiceCollection services)
    {
        services.AddScoped<CategorySeeder>();
        services.AddScoped<ClientSeeder>();
        services.AddScoped<DatabaseSeeder>();
        return services;
    }

    /// <summary>
    /// Runs the full seed pipeline.
    /// Call this from Program.cs in Development only.
    /// </summary>
    public static async Task SeedDatabaseAsync(this IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}