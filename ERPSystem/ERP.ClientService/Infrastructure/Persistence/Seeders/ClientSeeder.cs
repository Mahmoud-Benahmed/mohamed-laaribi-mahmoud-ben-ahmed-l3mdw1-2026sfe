using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Exceptions;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Application.Services;
using ERP.ClientService.Domain;
using Microsoft.Extensions.Logging;
using static ERP.ClientService.Properties.ApiRoutes;

namespace ERP.ClientService.Infrastructure.Persistence.Seeders;

public class ClientSeeder
{
    private readonly IClientRepository _clientRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ClientSeeder> _logger;
    private static readonly Guid SystemUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public ClientSeeder(
        IClientRepository clientRepository,
        ICategoryRepository categoryRepository,
        ITenantContext tenantContext,
        ILogger<ClientSeeder> logger)
    {
        _clientRepository = clientRepository;
        _categoryRepository = categoryRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task SeedAsync(List<Category> categories)
    {
        // Check if any clients already exist (using total count, not just first page)
        (_, int totalCount) = await _clientRepository.GetAllAsync(1, 10);
        if (totalCount > 0)
        {
            _logger.LogInformation("Clients already seeded — skipping.");
            return;
        }

        if (categories == null || !categories.Any())
        {
            _logger.LogError("No categories provided. Cannot seed clients without categories.");
            return;
        }

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not set for seeding clients.");

        Dictionary<string, Category> byCode = categories.ToDictionary(c => c.Code);
        List<Client> clientRequests = BuildClientRequests(byCode, tenantId);
        Random random = new Random();

        // Seed normal clients
        foreach (Client client in clientRequests)
        {
            try
            {
                int categoryCount = random.Next(categories.Count);
                var indices = Enumerable.Range(0, categories.Count)
                                        .OrderBy(x => random.Next())
                                        .Take(categoryCount)
                                        .ToList();

                foreach (int idx in indices)
                {
                    Category category = categories[idx];
                    try
                    {
                        client.AddCategory(category, SystemUserId);
                        _logger.LogInformation("  Assigned category '{Category}' to client {Client}",
                            category.Name, client.Name);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex, "  Could not assign category {CategoryName} to client {Client}",
                            category.Name, client.Name);
                    }
                }

                await _clientRepository.AddAsync(client);
                await _clientRepository.SaveChangesAsync();
                _logger.LogInformation("Seeded client: {Name} with {Count} categories",
                    client.Name, indices.Count);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "Failed to seed client: {Name}", client.Name);
            }
        }

        // Seed blocked and deleted clients once, after all normal clients
        await CreateBlockedClient(byCode, tenantId);
        await CreateDeletedClient(byCode, tenantId);

        _logger.LogInformation("Client seeding completed.");
    }

    private async Task CreateBlockedClient(Dictionary<string, Category> byCode, Guid tenantId)
    {
        const string blockedEmail = "riadh.mansouri@blocked.tn";
        // Avoid duplicate insertion
        if (await _clientRepository.GetByEmailAsync(blockedEmail) != null)
        {
            _logger.LogInformation("Blocked client already exists – skipping.");
            return;
        }

        try
        {
            Client blocked = Client.Create(
                name: "Riadh Mansouri",
                email: blockedEmail,
                address: "Bardo, Tunis 2000",
                duePaymentPeriod: 7,
                creditLimit: 10000m,
                phone: "+216 71 000 008",
                taxNumber: null,
                delaiRetour: 30,
                tenantId: tenantId);
            blocked.Block();
            await _clientRepository.AddAsync(blocked);
            await _clientRepository.SaveChangesAsync();
            _logger.LogInformation("Seeded and blocked client: {Name}", blocked.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed blocked client");
        }
    }

    private async Task CreateDeletedClient(Dictionary<string, Category> byCode, Guid tenantId)
    {
        const string deletedEmail = "contact@fantome.tn";
        if (await _clientRepository.GetByEmailAsync(deletedEmail) != null)
        {
            _logger.LogInformation("Deleted client already exists – skipping.");
            return;
        }

        try
        {
            Client deleted = Client.Create(
                name: "Société Fantôme",
                email: deletedEmail,
                address: "Adresse inconnue",
                duePaymentPeriod: 7,
                creditLimit: null,
                phone: null,
                taxNumber: null,
                delaiRetour: 30,
                tenantId: tenantId);
            deleted.Delete();
            await _clientRepository.AddAsync(deleted);
            await _clientRepository.SaveChangesAsync();
            _logger.LogInformation("Seeded and soft-deleted client: {Name}", deleted.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed deleted client");
        }
    }

    private List<Client> BuildClientRequests(Dictionary<string, Category> byCode, Guid tenantId)
    {
        List<Client> clients = new List<Client>();

        // 1. Standard retail client
        clients.Add(Client.Create(
            name: "Alice Martin",
            email: "alice.martin@example.com",
            address: "12 Rue de la Paix, Tunis 1001",
            duePaymentPeriod: 7,
            creditLimit: 5000m,
            phone: "+216 71 000 001",
            taxNumber: null,
            delaiRetour: 30,
            tenantId: tenantId));

        // 2. VIP client
        clients.Add(Client.Create(
            name: "Omar Ben Salah",
            email: "omar.bensalah@acmecorp.tn",
            address: "45 Avenue Habib Bourguiba, Sfax 3000",
            duePaymentPeriod: 45,
            creditLimit: 50000m,
            phone: "+216 74 000 002",
            taxNumber: "TN12345678",
            delaiRetour: 45,
            tenantId: tenantId));

        // 3. Wholesale company
        clients.Add(Client.Create(
            name: "Global Trade SARL",
            email: "contact@globaltrade.tn",
            address: "Zone Industrielle, Monastir 5000",
            duePaymentPeriod: 60,
            creditLimit: 200000m,
            phone: "+216 73 000 003",
            taxNumber: "TN98765432",
            delaiRetour: null,
            tenantId: tenantId));

        // 4. Public sector client
        clients.Add(Client.Create(
            name: "Ministère de l'Éducation",
            email: "achats@education.gov.tn",
            address: "Boulevard Bab Benat, Tunis 1008",
            duePaymentPeriod: 90,
            creditLimit: 500000m,
            phone: "+216 71 000 004",
            taxNumber: "TN00000001",
            delaiRetour: null,
            tenantId: tenantId));

        // 5. Reseller with two categories (categories will be assigned randomly later)
        clients.Add(Client.Create(
            name: "TechResell Pro",
            email: "info@techresell.tn",
            address: "Centre Urbain Nord, Tunis 1082",
            duePaymentPeriod: 45,
            creditLimit: 80000m,
            phone: "+216 71 000 005",
            taxNumber: "TN11223344",
            delaiRetour: null,
            tenantId: tenantId));

        // 6. New client
        clients.Add(Client.Create(
            name: "Yasmine Trabelsi",
            email: "yasmine.trabelsi@gmail.com",
            address: "Cité El Khadra, Tunis 1003",
            duePaymentPeriod: 15,
            creditLimit: 1000m,
            phone: null,
            taxNumber: null,
            delaiRetour: null,
            tenantId: tenantId));

        // 7. Client with personal DelaiRetour override
        clients.Add(Client.Create(
            name: "Karim Jebali",
            email: "karim.jebali@premium.tn",
            address: "Les Berges du Lac, Tunis 1053",
            duePaymentPeriod: 60,
            creditLimit: 30000m,
            phone: "+216 71 000 007",
            taxNumber: null,
            delaiRetour: 60,
            tenantId: tenantId));

        // 8. Client without any category
        clients.Add(Client.Create(
            name: "Slim Bouaziz",
            email: "slim.bouaziz@nocategory.tn",
            address: "Ariana 2080",
            duePaymentPeriod: 30,
            creditLimit: 0,
            phone: "+216 71 000 010",
            taxNumber: null,
            delaiRetour: null,
            tenantId: tenantId));

        return clients;
    }
}