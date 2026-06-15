namespace ERP.StockService.Infrastructure.Messaging.Events.ArticleEvents.TenantEvent;
public record TenantCreatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive
    );
public record TenantDeletedEvent(
    Guid TenantId,
    string Slug
    );