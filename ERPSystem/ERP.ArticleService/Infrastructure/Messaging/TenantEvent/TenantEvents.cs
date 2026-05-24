namespace ERP.ArticleService.Infrastructure.Messaging;
public record TenantCreatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive
    );