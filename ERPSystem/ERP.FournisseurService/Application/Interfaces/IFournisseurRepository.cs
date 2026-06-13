using ERP.FournisseurService.Application.DTOs;
using ERP.FournisseurService.Domain;
namespace ERP.FournisseurService.Application.Interfaces;

public interface IFournisseurRepository
{
    Task AddAsync(Fournisseur f);
    Task SaveChangesAsync();
    Task<bool> DuplicateExists(string email, string taxNum, string rib, Guid? excludeId=null);
    Task<Fournisseur?> GetByIdAsync(Guid id);
    Task<Fournisseur?> GetByIdDeletedAsync(Guid id);
    Task<(List<Fournisseur> Items, int TotalCount)> GetPagedByNameAsync(
    string nameFilter, int page, int size);
    Task<(List<Fournisseur> Items, int TotalCount)> GetAllAsync(int page, int size);
    Task<(List<Fournisseur> Items, int TotalCount)> GetPagedDeletedAsync(int page, int size);
    Task<FournisseurStatsDto> GetStatsAsync();
}

