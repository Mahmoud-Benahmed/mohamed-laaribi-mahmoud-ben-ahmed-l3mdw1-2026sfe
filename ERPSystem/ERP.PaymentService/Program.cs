using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ERP.PaymentService.Application.Interfaces;
using ERP.PaymentService.Application.Interfaces.LocalCache;
using ERP.PaymentService.Application.Services;
using ERP.PaymentService.Application.Services.LocalCache;
using ERP.PaymentService.Infrastructure.Messaging;
using ERP.PaymentService.Infrastructure.Messaging.Events;
using ERP.PaymentService.Infrastructure.Messaging.Events.Invoice;
using ERP.PaymentService.Infrastructure.Persistence;
using ERP.PaymentService.Infrastructure.Persistence.Repositories;
using ERP.PaymentService.Infrastructure.Persistence.Repositories.LocalCache;
using ERP.PaymentService.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// =========================
// DATABASE
// =========================
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(connectionString));

// =========================
// CONTROLLERS & SERIALIZATION
// =========================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// =========================
// API RESPONSE NORMALIZATION
// =========================
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        string message = string.Join(" | ", context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));

        return new BadRequestObjectResult(new
        {
            statusCode = 400,
            code = "VALIDATION_ERROR",
            message
        });
    };
});


var kafkaConfig = new ProducerConfig
{
    BootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
};

///////////////////////////////////////////////////
// Health Checks
///////////////////////////////////////////////////
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sql")
    .AddKafka(kafkaConfig)
    .AddCheck("self", () => HealthCheckResult.Healthy());

// =========================
// REPOSITORIES
// =========================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IRefundRequestRepository, RefundRequestRepository>();
builder.Services.AddScoped<IInvoiceCacheRepository, InvoiceCacheRepository>();
builder.Services.AddScoped<IPaymentInvoiceRepository, PaymentInvoiceRepository>();

// =========================
// SERVICES
// =========================
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IInvoiceCacheService, InvoiceCacheService>();
builder.Services.AddScoped<IPaymentNumberGenerator, PaymentNumberGenerator>();
builder.Services.AddScoped<IInvoiceEventHandler, InvoiceEventHandler>();
builder.Services.AddHostedService<InvoiceEventConsumer>();


builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

// =========================
// SWAGGER
// =========================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ====================================
// BUILD
// ====================================
WebApplication app = builder.Build();

// ====================================
// KAFKA TOPIC VERIFICATION & CREATION
// ====================================
using (IServiceScope scope = app.Services.CreateScope())
{
    IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    string bootstrapServers = configuration["Kafka:BootstrapServers"]
        ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured.");

    using IAdminClient adminClient = new AdminClientBuilder(
        new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

    string[] requiredTopics = [InvoiceTopics.Created, InvoiceTopics.Cancelled];

    IEnumerable<TopicSpecification> topicSpecs = requiredTopics.Select(topic =>
        new TopicSpecification
        {
            Name = topic,
            NumPartitions = 1,
            ReplicationFactor = 1
        });

    try
    {
        await adminClient.CreateTopicsAsync(topicSpecs);
        logger.LogInformation("Kafka topics created successfully.");
    }
    catch (CreateTopicsException ex)
        when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
    {
        logger.LogInformation("Kafka topics already exist, continuing.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating Kafka topics.");
    }

    // verify topics are ready
    int maxRetries = 30;
    TimeSpan retryDelay = TimeSpan.FromSeconds(2);

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            Metadata metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            HashSet<string> existingTopics = metadata.Topics.Select(t => t.Topic).ToHashSet();
            List<string> missing = requiredTopics.Where(t => !existingTopics.Contains(t)).ToList();

            if (!missing.Any())
            {
                logger.LogInformation("All Kafka topics are ready.");
                break;
            }

            logger.LogWarning(
                "Waiting for topics... Missing: {Missing}. Attempt {Attempt}/{Max}",
                string.Join(", ", missing), i + 1, maxRetries);

            await Task.Delay(retryDelay);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying Kafka topics. Attempt {Attempt}/{Max}", i + 1, maxRetries);
            await Task.Delay(retryDelay);
        }
    }
}

// =========================
// MIGRATIONS
// =========================
using (IServiceScope scope = app.Services.CreateScope())
{
    PaymentDbContext context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    await context.Database.MigrateAsync();
}

// =========================
// MIDDLEWARE PIPELINE
// =========================
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Name == "self"
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Name is "sql" or "kafka"
});

app.Run();