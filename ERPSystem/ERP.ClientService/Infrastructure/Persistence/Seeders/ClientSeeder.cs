using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Exceptions;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Domain;
using static ERP.ClientService.Properties.ApiRoutes;

namespace ERP.ClientService.Infrastructure.Persistence.Seeders;

public class ClientSeeder
{
    private readonly IClientRepository _clientRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<ClientSeeder> _logger;
    private static readonly Guid SystemUserId = Guid.NewGuid();

    public ClientSeeder(
        IClientRepository clientRepository,
        ICategoryRepository categoryRepository,
        ILogger<ClientSeeder> logger)
    {
        _clientRepository = clientRepository;
        _categoryRepository = categoryRepository;
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

        Dictionary<string, Category> byCode = categories.ToDictionary(c => c.Code);
        List<Client> clientRequests = BuildClientRequests(byCode);
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
        await CreateBlockedClient(byCode);
        await CreateDeletedClient(byCode);

        _logger.LogInformation("Client seeding completed.");
    }

    private async Task CreateBlockedClient(Dictionary<string, Category> byCode)
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
                "Riadh Mansouri",
                blockedEmail,
                "Bardo, Tunis 2000",
                10000m,
                "+216 71 000 008",
                null,
                null,
                30);
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

    private async Task CreateDeletedClient(Dictionary<string, Category> byCode)
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
                "Société Fantôme",
                deletedEmail,
                "Adresse inconnue",
                null,
                null,
                null,
                null,
                30);
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

    private List<Client> BuildClientRequests(Dictionary<string, Category> byCode)
    {
        List<Client> clients = new List<Client>();

        // Helper to get category ID by code
        Guid? GetCategoryId(string code) => byCode.TryGetValue(code, out Category? cat) ? cat.Id : null;

        // 1. Standard retail client
        clients.Add(Client.Create(
            "Alice Martin",
            "alice.martin@example.com",
            "12 Rue de la Paix, Tunis 1001",
            5000m,
            "+216 71 000 001",
            null,
            null,
            30));
        // 2. VIP client
        clients.Add(Client.Create(
            "Omar Ben Salah",
            "omar.bensalah@acmecorp.tn",
            "45 Avenue Habib Bourguiba, Sfax 3000",
            50000m,
            "+216 74 000 002",
            "TN12345678",
            null,
            45));

        // 3. Wholesale company
        clients.Add(Client.Create(
            "Global Trade SARL",
            "contact@globaltrade.tn",
            "Zone Industrielle, Monastir 5000",
            200000m,
            "+216 73 000 003",
            "TN98765432",
            null,
            60));

        // 4. Public sector client
        clients.Add(Client.Create(
            "Ministère de l'Éducation",
            "achats@education.gov.tn",
            "Boulevard Bab Benat, Tunis 1008",
            500000m,
            "+216 71 000 004",
            "TN00000001",
            null,
            90));

        // 5. Reseller with two categories
        List<Guid> resellerCategories = new List<Guid>();
        if (GetCategoryId("RSL").HasValue) resellerCategories.Add(GetCategoryId("RSL").Value);
        if (GetCategoryId("WHL").HasValue) resellerCategories.Add(GetCategoryId("WHL").Value);

        clients.Add(Client.Create(
            "TechResell Pro",
            "info@techresell.tn",
            "Centre Urbain Nord, Tunis 1082",
            80000m,
            "+216 71 000 005",
            "TN11223344",
            null,
            45
        ));

        // 6. New client
        clients.Add(Client.Create(
            "Yasmine Trabelsi",
            "yasmine.trabelsi@gmail.com",
            "Cité El Khadra, Tunis 1003",
            1000m,
            null,
            null,
            null,
            15));

        // 7. Client with personal DelaiRetour override
        clients.Add(Client.Create(
            "Karim Jebali",
            "karim.jebali@premium.tn",
            "Les Berges du Lac, Tunis 1053",
            30000m,
            "+216 71 000 007",
            null,
            90,
            60));

        // 8. Client without any category
        clients.Add(Client.Create(
            "Slim Bouaziz",
            "slim.bouaziz@nocategory.tn",
            "Ariana 2080",
            null,
            "+216 71 000 010",
            null,
            null,
            30));

        return clients;
    }
}