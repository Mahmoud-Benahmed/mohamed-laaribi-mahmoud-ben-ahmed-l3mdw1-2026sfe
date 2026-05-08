namespace ERP.ClientService.Infrastructure.Persistence;

public class TenantConnectionString
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public TenantConnectionString(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public string Resolve()
    {
        var slug = _httpContextAccessor.HttpContext?.Items["TenantSlug"]?.ToString();

        if (string.IsNullOrEmpty(slug))
            return _configuration.GetConnectionString("DefaultConnection")!;

        return $"Server=localhost;Database=ERPClientsDb_{slug};Integrated Security=True;TrustServerCertificate=True;";
    }
}