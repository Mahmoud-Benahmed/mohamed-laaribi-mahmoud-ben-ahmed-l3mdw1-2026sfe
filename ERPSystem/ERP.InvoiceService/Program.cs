using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Application.Services;
using ERP.InvoiceService.Application.Services.LocalCache;
using ERP.InvoiceService.Application.Services.LocalCache.ArticleCache;
using ERP.InvoiceService.Application.Services.LocalCache.ClientCache;
using ERP.InvoiceService.Infrastructure.Messaging;
using ERP.InvoiceService.Infrastructure.Messaging.Events;
using ERP.InvoiceService.Infrastructure.Messaging.Events.ArticleEvents.Article;
using ERP.InvoiceService.Infrastructure.Messaging.Events.ArticleEvents.ArticleCategory;
using ERP.InvoiceService.Infrastructure.Messaging.Events.ClientEvents.Category;
using ERP.InvoiceService.Infrastructure.Messaging.Events.ClientEvents.Client;
using ERP.InvoiceService.Infrastructure.Messaging.Events.Payment;
using ERP.InvoiceService.Infrastructure.Messaging.Events.TenantEvent;
using ERP.InvoiceService.Infrastructure.Persistence;
using ERP.InvoiceService.Infrastructure.Persistence.Repositories;
using ERP.InvoiceService.Infrastructure.Persistence.Repositories.LocalCache;
using ERP.InvoiceService.Infrastructure.Persistence.Repositories.LocalCache.ArticleCache;
using ERP.InvoiceService.Infrastructure.Persistence.Repositories.LocalCache.ClientCache;
using InvoiceService.Application.Interfaces;
using InvoiceService.Middleware;
using InvoiceService.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.EnableDebugging = true;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// =========================
// DATABASE
// =========================
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<InvoiceDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });


builder.Services.AddHttpClient<IStockServiceHttpClient, StockServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:StockService:BaseUrl"]
        ?? throw new InvalidOperationException(
            "StockServiceUrl not configured."));
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
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();

// =========================
// SERVICES
// =========================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ITenantCacheRepository, TenantCacheRepository>();
builder.Services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
builder.Services.AddScoped<IInvoicesService, InvoicesService>();

// Article cache dependencies
builder.Services.AddScoped<IArticleCacheRepository, ArticleCacheRepository>();
builder.Services.AddScoped<IArticleCacheService, ArticleCacheService>();
builder.Services.AddScoped<IArticleEventHandler, ArticleEventHandler>();
builder.Services.AddHostedService<ArticleEventConsumer>();

// Article categories cache dependencies
builder.Services.AddScoped<IArticleCategoryCacheRepository, ArticleCategoryCacheRepository>();
builder.Services.AddScoped<IArticleCategoryCacheService, ArticleCategoryCacheService>();
builder.Services.AddScoped<IArticleCategoryEventHandler, ArticleCategoryEventHandler>();
builder.Services.AddHostedService<ArticleCategoryEventConsumer>();

// Client cache dependencies
builder.Services.AddScoped<IClientCacheRepository, ClientCacheRepository>();
builder.Services.AddScoped<IClientCacheService, ClientCacheService>();
builder.Services.AddScoped<IClientEventHandler, ClientEventHandler>();
builder.Services.AddHostedService<ClientEventConsumer>();

// Client category cache dependencies
builder.Services.AddScoped<IClientCategoryCacheRepository, ClientCategoryCacheRepository>();
builder.Services.AddScoped<IClientCategoryCacheService, ClientCategoryCacheService>();
builder.Services.AddScoped<IClientCategoryEventHandler, ClientCategoryEventHandler>();
builder.Services.AddHostedService<ClientCategoryEventConsumer>();

builder.Services.AddScoped<IPaymentEventHandler, PaymentEventHandler>();
builder.Services.AddHostedService<PaymentEventConsumer>();


builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddHostedService<TenantLifecycleConsumer>();
builder.Services.AddScoped<IInvoicePdfGenerator, InvoicePdfGenerator>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

builder.Services.AddHostedService<OverdueInvoiceJob>();

// =========================
// CONTROLLERS & API
// =========================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

WebApplication app = builder.Build();


// =========================
// KAFKA TOPIC VERIFICATION & CREATION
// =========================
using (IServiceScope scope = app.Services.CreateScope())
{
    IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    string bootstrapServers = configuration["Kafka:BootstrapServers"]
        ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured.");

    AdminClientConfig adminConfig = new AdminClientConfig
    {
        BootstrapServers = bootstrapServers
    };

    using IAdminClient adminClient = new AdminClientBuilder(adminConfig).Build();

    string[] requiredTopics = new[] {
        ArticleTopics.Created, ArticleTopics.Updated,
        ArticleTopics.Deleted, ArticleTopics.Restored,

        ArticleCategoryTopics.Created, ArticleCategoryTopics.Updated,
        ArticleCategoryTopics.Deleted, ArticleCategoryTopics.Restored,

        ClientTopics.Created, ClientTopics.Updated,
        ClientTopics.Deleted, ClientTopics.Restored,

        ClientCategoryTopics.Created, ClientCategoryTopics.Updated,
        ClientCategoryTopics.Deleted, ClientCategoryTopics.Restored,

        PaymentTopics.Cancelled, PaymentTopics.InvoicePaid,

        TenantTopics.TenantCreated,
        TenantTopics.TenantUpdated,
        TenantTopics.TenantDeleted
    };

    int maxRetries = 30;
    TimeSpan retryDelay = TimeSpan.FromSeconds(2);

    // First, try to create all topics
    IEnumerable<TopicSpecification> topicSpecifications = requiredTopics.Select(topic => new TopicSpecification
    {
        Name = topic,
        NumPartitions = 1,  // Adjust based on your needs
        ReplicationFactor = 1  // Adjust for your Kafka cluster
    });

    try
    {
        await adminClient.CreateTopicsAsync(topicSpecifications);
        logger.LogInformation("Successfully created all required Kafka topics");
    }
    catch (CreateTopicsException ex) when (ex.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
    {
        logger.LogInformation("Some topics already exist, continuing...");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating Kafka topics");
    }

    // Then verify they exist
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            Metadata metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            HashSet<string> existingTopics = metadata.Topics.Select(t => t.Topic).ToHashSet();

            List<string> missingTopics = requiredTopics.Where(t => !existingTopics.Contains(t)).ToList();

            if (!missingTopics.Any())
            {
                logger.LogInformation("All required Kafka topics exist and are ready");
                break;
            }

            logger.LogWarning("Waiting for topics to be fully created... Missing: {MissingTopics}. Attempt {Attempt}/{MaxRetries}",
                string.Join(", ", missingTopics), i + 1, maxRetries);
            await Task.Delay(retryDelay);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking Kafka topics. Attempt {Attempt}/{MaxRetries}", i + 1, maxRetries);
            await Task.Delay(retryDelay);
        }
    }
}


app.UseExceptionHandler(
    errApp => errApp.Run(async ctx =>
    {
        IExceptionHandlerFeature? feature = ctx.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error is InvoiceDomainException domainEx)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = domainEx.Message });
        }
    })
);

// =========================
// MIGRATIONS
// =========================
using (IServiceScope scope = app.Services.CreateScope())
{
    InvoiceDbContext context = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();

    await context.Database.MigrateAsync();
}

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