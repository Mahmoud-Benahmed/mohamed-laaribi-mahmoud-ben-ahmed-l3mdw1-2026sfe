namespace ERP.TenantService.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(string topic, object payload);
}