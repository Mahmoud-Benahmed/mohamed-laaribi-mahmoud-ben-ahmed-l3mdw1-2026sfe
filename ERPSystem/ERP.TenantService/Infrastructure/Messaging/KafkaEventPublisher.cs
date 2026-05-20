using Confluent.Kafka;
using System.Diagnostics;
using System.Text.Json;

namespace ERP.TenantService.Infrastructure.Messaging;

public class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public KafkaEventPublisher(IConfiguration configuration, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        ProducerConfig config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured."),
            ClientId = $"tenant-service-{Guid.NewGuid()}",
            EnableDeliveryReports = true,  // Enable delivery reports
            Acks = Acks.All,               // Wait for all replicas
            MessageTimeoutMs = 30000,       // 30 seconds
            EnableIdempotence = true        // Prevent duplicates
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
                _logger.LogError("Kafka error: {Error}", error))
            .SetLogHandler((_, log) =>
                _logger.LogDebug("Kafka log: {Message}", log.Message))
            .Build();
    }

    public async Task PublishAsync<T>(string topic, T @event)
    {
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Guid eventId = Guid.NewGuid();
            string json = JsonSerializer.Serialize(@event, _jsonOptions);

            Message<string, string> message = new Message<string, string>
            {
                Key = eventId.ToString(),
                Value = json,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };

            _logger.LogInformation(
                "Publishing event to Kafka - Topic: {Topic}, EventId: {EventId}, Type: {EventType}, Size: {Size} bytes",
                topic, eventId, typeof(T).Name, json.Length);

            // Log the actual content (only in development)
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                _logger.LogDebug("Event payload: {Json}", json);
            }

            // Produce with delivery report
            DeliveryResult<string, string> deliveryResult = await _producer.ProduceAsync(topic, message);

            stopwatch.Stop();

            _logger.LogInformation(
                "Event published successfully - Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, " +
                "EventId: {EventId}, Duration: {Duration}ms",
                topic, deliveryResult.Partition, deliveryResult.Offset, eventId, stopwatch.ElapsedMilliseconds);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex,
                "Failed to publish event to {Topic} - Error: {ErrorCode}, Reason: {Reason}",
                topic, ex.Error.Code, ex.Error.Reason);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing event to {Topic}", topic);
            throw;
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Flushing Kafka producer...");
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _logger.LogInformation("Kafka producer disposed.");
    }
}