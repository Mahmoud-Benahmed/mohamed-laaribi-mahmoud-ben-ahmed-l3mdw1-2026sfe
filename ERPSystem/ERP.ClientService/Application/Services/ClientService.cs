using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Exceptions;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Domain;
using ERP.ClientService.Infrastructure.Messaging;

namespace ERP.ClientService.Application.Services;

public class ClientService : IClientService
{
    private readonly IClientRepository _clientRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ClientService> _logger;

    public ClientService(IClientRepository clientRepository,
                        ICategoryRepository categoryRepository,
                        IEventPublisher eventPublisher,
                        ITenantContext tenantContext,
                        ILogger<ClientService> logger)
    {
        _clientRepository = clientRepository;
        _categoryRepository = categoryRepository;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<ClientResponseDto> CreateAsync(CreateClientRequestDto request)
    {
        if (await _clientRepository.DuplicateExists(request.Email, request.Phone))
            throw new DuplicateKeyException($"Client.Email: {request.Email} | Client.Phone: {request.Phone}");


        Client client = Client.Create(
            name: request.Name,
            email: request.Email,
            address: request.Address,
            phone: request.Phone,
            taxNumber: request.TaxNumber,
            creditLimit: request.CreditLimit,
            delaiRetour: request.DelaiRetour,
            duePaymentPeriod: request.DuePaymentPeriod,
            tenantId: _tenantContext.TenantId);

        await _clientRepository.AddAsync(client);
        await _clientRepository.SaveChangesAsync();

        ClientResponseDto res = client.ToResponseDto();
        await _eventPublisher.PublishAsync(ClientTopics.Created, res with { CreditLimit = res.EffectiveCreditLimit, DelaiRetour = res.EffectiveDelaiRetour });
        return res;
    }

    // =========================
    // READ
    // =========================
    public async Task<ClientResponseDto> GetByIdAsync(Guid id)
    {
        Client client = await _clientRepository.GetByIdAsync(id) ?? throw new ClientNotFoundException(id);
        return client.ToResponseDto();
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<ClientResponseDto> UpdateAsync(Guid id, UpdateClientRequestDto request)
    {
        Client client = await _clientRepository.GetByIdAsync(id) ?? throw new ClientNotFoundException(id);

        string normalised = request.Email.Trim().ToLowerInvariant();
        if (await _clientRepository.DuplicateExists(request.Email, request.Phone, id))
            throw new DuplicateKeyException($"Client.Email: {request.Email} | Client.Phone: {request.Phone}");

        client.Update(request.Name, request.Email, request.Address,
            request.Phone, request.TaxNumber);

        if (request.DuePaymentPeriod is > 0)        // ← pattern match guards against 0
            client.SetDuePaymentPeriod(request.DuePaymentPeriod.Value);
        else
            client.ClearDuePaymentPeriod();

        if (request.CreditLimit.HasValue)
            client.SetCreditLimit(request.CreditLimit.Value);
        else
            client.RemoveCreditLimit();

        if (request.DelaiRetour.HasValue)
            client.SetDelaiRetour(request.DelaiRetour.Value);
        else
            client.ClearDelaiRetour();

        await _clientRepository.SaveChangesAsync();
        ClientResponseDto res = client.ToResponseDto();
        // Publish effective values — consumers use these for business rules, not raw limits
        await _eventPublisher.PublishAsync(ClientTopics.Updated, res with {CreditLimit= res.EffectiveCreditLimit, DelaiRetour= res.EffectiveDelaiRetour });
        return res;
    }

    // =========================
    // DELETE
    // =========================
    public async Task DeleteAsync(Guid id)
    {
        Client client = await _clientRepository.GetByIdAsync(id) ?? throw new ClientNotFoundException(id);

        client.Delete();
        await _clientRepository.SaveChangesAsync();
        ClientResponseDto res = client.ToResponseDto();
        await _eventPublisher.PublishAsync(ClientTopics.Deleted, res with { CreditLimit = res.EffectiveCreditLimit, DelaiRetour = res.EffectiveDelaiRetour });
    }

    // =========================
    // RESTORE
    // =========================
    public async Task RestoreAsync(Guid id)
    {
        Client client = await _clientRepository.GetByIdDeletedAsync(id) ?? throw new ClientNotFoundException(id);

        if (!client.IsDeleted)
            return;

        client.Restore();
        await _clientRepository.SaveChangesAsync();
        ClientResponseDto res = client.ToResponseDto();
        await _eventPublisher.PublishAsync(ClientTopics.Restored, res with { CreditLimit = res.EffectiveCreditLimit, DelaiRetour = res.EffectiveDelaiRetour });
    }

    // =========================
    // BLOCK / UNBLOCK
    // =========================
    public async Task<ClientResponseDto> BlockAsync(Guid id)
    {
        Client client = await _clientRepository.GetByIdAsync(id) ?? throw new ClientNotFoundException(id);

        client.Block();
        await _clientRepository.SaveChangesAsync();
        ClientResponseDto res = client.ToResponseDto();
        await _eventPublisher.PublishAsync(ClientTopics.Updated, res with { CreditLimit = res.EffectiveCreditLimit, DelaiRetour = res.EffectiveDelaiRetour });
        return res;
    }

    public async Task<ClientResponseDto> UnblockAsync(Guid id)
    {
        Client client = await _clientRepository.GetByIdAsync(id) ?? throw new ClientNotFoundException(id);

        client.Unblock();
        await _clientRepository.SaveChangesAsync();
        ClientResponseDto res = client.ToResponseDto();
        await _eventPublisher.PublishAsync(ClientTopics.Updated, res with { CreditLimit = res.EffectiveCreditLimit, DelaiRetour = res.EffectiveDelaiRetour });
        return res;
    }


    // =========================
    // CATEGORY ASSIGNMENT
    // =========================
    public async Task<ClientResponseDto> AddCategoryAsync(
        Guid clientId, Guid categoryId, Guid assignedById)
    {

        _logger.LogWarning($"\n\n\nREquesterId: {assignedById}\n\n\n");

        Client client = await _clientRepository.GetByIdAsync(clientId) ?? throw new ClientNotFoundException(clientId);

        Category category = await _categoryRepository.GetByIdAsync(categoryId) ?? throw new CategoryNotFoundException(categoryId);
        
        client.AddCategory(category, assignedById);
        await _clientRepository.SaveChangesAsync();
        ClientResponseDto res = client.ToResponseDto();
        await _eventPublisher.PublishAsync(ClientTopics.Updated, res with { CreditLimit = res.EffectiveCreditLimit, DelaiRetour = res.EffectiveDelaiRetour });
        return res;
    }

    public async Task<ClientResponseDto> RemoveCategoryAsync(Guid clientId, Guid categoryId)
    {
        Client client = await _clientRepository.GetByIdAsync(clientId) ?? throw new ClientNotFoundException(clientId);

        Category category = await _categoryRepository.GetByIdAsync(categoryId)
            ?? throw new CategoryNotFoundException(categoryId);

        client.RemoveCategory(category);

        await _clientRepository.SaveChangesAsync();
        ClientResponseDto res = client.ToResponseDto();
        await _eventPublisher.PublishAsync(ClientTopics.Updated, res with { CreditLimit = res.EffectiveCreditLimit, DelaiRetour = res.EffectiveDelaiRetour });
        return res;
    }

    // =========================
    // PAGING / FILTERING
    // =========================
    public async Task<PagedResultDto<ClientResponseDto>> GetAllAsync(
        int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        (List<Client>? items, int totalCount) = await _clientRepository.GetAllAsync(pageNumber, pageSize);
        return new PagedResultDto<ClientResponseDto>(
            items.Select(c => c.ToResponseDto()).ToList(), totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<ClientResponseDto>> GetPagedDeletedAsync(
        int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        (List<Client>? items, int totalCount) = await _clientRepository
            .GetPagedDeletedAsync(pageNumber, pageSize);
        return new PagedResultDto<ClientResponseDto>(
            items.Select(c => c.ToResponseDto()).ToList(), totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<ClientResponseDto>> GetPagedByCategoryIdAsync(
        Guid categoryId, int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        (List<Client>? items, int totalCount) = await _clientRepository
            .GetPagedByCategoryIdAsync(categoryId, pageNumber, pageSize);
        return new PagedResultDto<ClientResponseDto>(
            items.Select(c => c.ToResponseDto()).ToList(), totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<ClientResponseDto>> GetPagedByNameAsync(
        string nameFilter, int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        if (string.IsNullOrWhiteSpace(nameFilter))
            throw new ArgumentException("Name filter cannot be empty.");

        (List<Client>? items, int totalCount) = await _clientRepository
            .GetPagedByNameAsync(nameFilter, pageNumber, pageSize);
        return new PagedResultDto<ClientResponseDto>(
            items.Select(c => c.ToResponseDto()).ToList(), totalCount, pageNumber, pageSize);
    }

    // =========================
    // STATS
    // =========================
    public async Task<ClientStatsDto> GetStatsAsync() =>
        await _clientRepository.GetStatsAsync();

    // =========================
    // DOMAIN QUERIES
    // =========================
    public async Task<int?> GetEffectiveDelaiRetourAsync(Guid id)
    {
        Client client = await _clientRepository.GetByIdAsync(id) ?? throw new ClientNotFoundException(id);
        return client.GetEffectiveDelaiRetour();
    }

    public async Task<bool> CanPlaceOrderAsync(
        Guid id, decimal orderAmount, decimal currentBalance)
    {
        Client client = await _clientRepository.GetByIdAsync(id) ?? throw new ClientNotFoundException(id);
        return client.CanPlaceOrder(orderAmount, currentBalance);
    }

    // =========================
    // PRIVATE HELPERS
    // =========================
    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber),
                "Page number must be greater than zero.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize),
                "Page size must be greater than zero.");
    }
}