using ERP.FournisseurService.Application.DTOs;
using ERP.FournisseurService.Application.Interfaces;
using ERP.FournisseurService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.FournisseurService.Infrastructure.Persistence.Seeders;

public class FournisseurSeeder
{
    private readonly FournisseurDbContext _context;
    private readonly ILogger<FournisseurSeeder> _logger;
    private readonly IFournisseurRepository _fournisseurRepository;

    public FournisseurSeeder(FournisseurDbContext context,
                            ILogger<FournisseurSeeder> logger,
                            IFournisseurRepository fournisseurRepository)
    {
        _context = context;
        _logger = logger;
        _fournisseurRepository = fournisseurRepository;
    }

    public async Task SeedAsync()
    {
        if (await _context.Fournisseurs.AnyAsync())
        {
            _logger.LogInformation("Fournisseurs already seeded — skipping.");
            return;
        }

        List<Fournisseur> fournisseurs = BuildFournisseurs();
        foreach (Fournisseur f in fournisseurs)
        {
            await _fournisseurRepository.AddAsync(f);
        }
        
        await _fournisseurRepository.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} fournisseurs.", fournisseurs.Count);
    }

    // ── Seed data ─────────────────────────────────────────────────────────────

    private static List<Fournisseur> BuildFournisseurs()
    {
        List<Fournisseur> fournisseurs = new List<Fournisseur>();

        // ── 1. Standard local supplier ─────────────────────────────────────────
        Fournisseur techSupply = Fournisseur.Create(
            name: "TechSupply Tunisie",
            address: "Zone Industrielle Ben Arous, Tunis 2013",
            phone: "+216 71 100 001",
            taxNumber: "TN10000001",
            rib: "TN5901000000000000000001",
            email: "contact@techsupply.tn");

        fournisseurs.Add(techSupply);

        // ── 2. International supplier ──────────────────────────────────────────
        Fournisseur globalElec = Fournisseur.Create(
            name: "Global Electronics SARL",
            address: "45 Rue du Commerce, Sfax 3000",
            phone: "+216 74 100 002",
            taxNumber: "TN10000002",
            rib: "TN5901000000000000000002",
            email: "orders@globalelec.tn");

        fournisseurs.Add(globalElec);

        // ── 3. Raw materials supplier ──────────────────────────────────────────
        Fournisseur matierePremiere = Fournisseur.Create(
            name: "Matières Premières du Nord",
            address: "Route de Bizerte Km 12, Tunis 1080",
            phone: "+216 71 100 003",
            taxNumber: "TN10000003",
            rib: "TN5901000000000000000003",
            email: "approvisionnement@mpnord.tn");

        fournisseurs.Add(matierePremiere);

        // ── 4. Packaging supplier — no email ──────────────────────────────────
        Fournisseur packPro = Fournisseur.Create(
            name: "PackPro Industrie",
            address: "Centre Industriel, Sousse 4000",
            phone: "+216 73 100 004",
            taxNumber: "TN10000004",
            rib: "TN5901000000000000000004",
            email: null);

        fournisseurs.Add(packPro);

        // ── 5. IT services supplier ────────────────────────────────────────────
        Fournisseur itServices = Fournisseur.Create(
            name: "IT Solutions Pro",
            address: "Les Berges du Lac II, Tunis 1053",
            phone: "+216 71 100 005",
            taxNumber: "TN10000005",
            rib: "TN5901000000000000000005",
            email: "support@itsolutionspro.tn");

        fournisseurs.Add(itServices);

        // ── 6. Blocked supplier — payment dispute ──────────────────────────────
        Fournisseur blockedSupplier = Fournisseur.Create(
            name: "Fournisseur Bloqué SARL",
            address: "Bardo, Tunis 2000",
            phone: "+216 71 100 006",
            taxNumber: "TN10000006",
            rib: "TN5901000000000000000006",
            email: "info@bloque.tn");

        blockedSupplier.Block();
        fournisseurs.Add(blockedSupplier);

        // ── 7. Soft-deleted supplier — to test GetPagedDeleted ─────────────────
        Fournisseur deletedSupplier = Fournisseur.Create(
            name: "Société Dissoute",
            address: "Adresse inconnue",
            phone: "+216 71 100 007",
            taxNumber: "TN10000007",
            rib: "TN5901000000000000000007",
            email: null);

        deletedSupplier.Delete();
        fournisseurs.Add(deletedSupplier);

        // ── 8. Wholesale food supplier ─────────────────────────────────────────
        Fournisseur foodSupply = Fournisseur.Create(
            name: "AgroFood Distribution",
            address: "Marché de Gros, Bir El Kassaa 2059",
            phone: "+216 71 100 008",
            taxNumber: "TN10000008",
            rib: "TN5901000000000000000008",
            email: "commandes@agrofood.tn");

        fournisseurs.Add(foodSupply);

        // ── 9. Office supplies supplier ────────────────────────────────────────
        Fournisseur officePro = Fournisseur.Create(
            name: "OfficePro Fournitures",
            address: "Avenue de la Liberté, Tunis 1002",
            phone: "+216 71 100 009",
            taxNumber: "TN10000009",
            rib: "TN5901000000000000000009",
            email: "ventes@officepro.tn");

        fournisseurs.Add(officePro);

        // ── 10. Logistics / transport supplier ────────────────────────────────
        Fournisseur logistix = Fournisseur.Create(
            name: "Logistix Transport",
            address: "Route Nationale 1, Hammam Lif 2050",
            phone: "+216 71 100 010",
            taxNumber: "TN10000010",
            rib: "TN5901000000000000000010",
            email: "logistics@logistix.tn");

        fournisseurs.Add(logistix);

        return fournisseurs;
    }
}