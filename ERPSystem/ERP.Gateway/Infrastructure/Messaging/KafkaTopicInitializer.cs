namespace ERP.Gateway.Infrastructure.Messaging;

using Confluent.Kafka;
using Confluent.Kafka.Admin;


public sealed class KafkaTopicInitializer : IHostedService
{
    private readonly IConfiguration _config;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    private static readonly string[] Topics =
    [
        TenantTopics.TenantCreated,
        TenantTopics.TenantUpdated,
        TenantTopics.TenantDeleted,
        TenantTopics.TenantRestored,
        TenantTopics.TenantActivated,
        TenantTopics.TenantDeactivated
    ];

    public KafkaTopicInitializer(
        IConfiguration config,
        ILogger<KafkaTopicInitializer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var bootstrapServers = _config["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured.");

        using var adminClient = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = bootstrapServers })
            .Build();

        try
        {
            await adminClient.CreateTopicsAsync(Topics.Select(t =>
                new TopicSpecification
                {
                    Name = t,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }));

            _logger.LogInformation(
                "Kafka topics ensured: {Topics}", string.Join(", ", Topics));
        }
        catch (CreateTopicsException ex)
        {
            foreach (var result in ex.Results)
            {
                // TopicAlreadyExists is fine — idempotent
                if (result.Error.Code != ErrorCode.TopicAlreadyExists)
                {
                    _logger.LogError(
                        "Failed to create topic {Topic}: {Reason}",
                        result.Topic, result.Error.Reason);
                }
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}