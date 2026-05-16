using InvoiceService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.InvoiceService.Infrastructure.Persistence;

public class InvoiceDbContextFactory : IDesignTimeDbContextFactory<InvoiceDbContext>
{
    public InvoiceDbContext CreateDbContext(string[] args)
    {
        // Walk up from the output dir to find the startup project's appsettings.json
        var basePath = Path.Combine(
            Directory.GetCurrentDirectory()
        );

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<InvoiceDbContext>();

        // Adjust the key to match your appsettings.json connection string name
        optionsBuilder.UseSqlServer(
            config.GetConnectionString("DefaultConnection")
        );

        return new InvoiceDbContext(optionsBuilder.Options);
    }
}