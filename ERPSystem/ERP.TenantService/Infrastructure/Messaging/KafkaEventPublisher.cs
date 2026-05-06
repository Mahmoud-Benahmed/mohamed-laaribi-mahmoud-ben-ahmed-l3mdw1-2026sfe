using Confluent.Kafka;
using System.Text.Json;

namespace ERP.TenantService.Infrastructure.Messaging;

public class KafkaEventPublisher : IEventPublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IConfiguration config, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            AllowAutoCreateTopics = true 
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    public async Task PublishAsync(string topic, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var message = new Message<Null, string> { Value = json };
        await _producer.ProduceAsync(topic, message);
        _logger.LogInformation("Published event to topic '{Topic}': {Payload}", topic, json);
    }
}