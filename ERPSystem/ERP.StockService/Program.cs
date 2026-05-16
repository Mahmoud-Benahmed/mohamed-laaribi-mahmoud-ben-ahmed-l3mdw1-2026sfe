
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Application.Services;
using ERP.StockService.Application.Services.LocalCache;
using ERP.StockService.Application.Services.LocalCache.ArticleCache;
using ERP.StockService.Application.Services.LocalCache.ClientCache;
using ERP.StockService.Application.Services.LocalCache.Fournisseur;
using ERP.StockService.Application.Services.LocalCache.InvoiceCache;
using ERP.StockService.Infrastructure.Messaging.Events.ArticleEvents.Article;
using ERP.StockService.Infrastructure.Messaging.Events.ArticleEvents.Category;
using ERP.StockService.Infrastructure.Messaging.Events.ClientEvents.Category;
using ERP.StockService.Infrastructure.Messaging.Events.ClientEvents.Client;
using ERP.StockService.Infrastructure.Messaging.Events.FournisseurEvents;
using ERP.StockService.Infrastructure.Messaging.Events.InvoiceEvents;
using ERP.StockService.Infrastructure.Persistence;
using ERP.StockService.Infrastructure.Persistence.Repositories;
using ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache;
using ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache.ArticleCache;
using ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache.ClientCache;
using ERP.StockService.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// =========================
// DATABASE
// =========================
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionString 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<StockDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Unified validation error response — Data Annotations return this shape
        options.InvalidModelStateResponseFactory = context =>
        {
            string message = string.Join(" | ", context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            return new BadRequestObjectResult(new
            {
                statusCode = 400,
                code = "VALIDATION ERROR",
                message
            });
        };
    }); ;


// =========================
// DEPENDENCY INJECTION
// =========================

// Add services to the container.
builder.Services.AddScoped<IBonEntreRepository, BonEntreRepository>();
builder.Services.AddScoped<IBonSortieRepository, BonSortieRepository>();
builder.Services.AddScoped<IBonRetourRepository, BonRetourRepository>();

builder.Services.AddScoped<IBonNumeroRepository, BonNumeroRepository>();
builder.Services.AddScoped<IBonEntreService, BonEntreService>();
builder.Services.AddScoped<IBonSortieService, BonSortieService>();
builder.Services.AddScoped<IBonRetourService, BonRetourService>();
builder.Services.AddScoped<IJournalStockRepository, JournalStockRepository>();

// Article cache dependencies
builder.Services.AddScoped<IArticleCacheRepository, ArticleCacheRepository>();
builder.Services.AddScoped<IArticleCacheService, ArticleCacheService>();
builder.Services.AddScoped<IArticleEventHandler, ArticleEventHandler>();
builder.Services.AddHostedService<ArticleEventConsumer>();

// Category cache dependencies
builder.Services.AddScoped<IArticleCategoryCacheRepository, ArticleCategoryCacheRepository>();
builder.Services.AddScoped<IArticleCategoryCacheService, ArticleCategoryCacheService>();
builder.Services.AddScoped<IArticleCategoryEventHandler, ArticleCategoryEventHandler>();
builder.Services.AddHostedService<ArticleCategoryEventConsumer>();

// Client cache dependencies
builder.Services.AddScoped<IClientCacheRepository, ClientCacheRepository>();
builder.Services.AddScoped<IClientCacheService, ClientCacheService>();
builder.Services.AddScoped<IClientEventHandler, ClientEventHandler>();
builder.Services.AddHostedService<ClientEventConsumer>();

builder.Services.AddScoped<IClientCategoryCacheRepository, ClientCategoryCacheRepository>();
builder.Services.AddScoped<IClientCategoryCacheService, ClientCategoryCacheService>();
builder.Services.AddScoped<IClientCategoryEventHandler, ClientCategoryEventHandler>();
builder.Services.AddHostedService<ClientCategoryEventConsumer>();

builder.Services.AddScoped<IFournisseurCacheRepository, FournisseurCacheRepository>();
builder.Services.AddScoped<IFournisseurCacheService, FournisseurCacheService>();
builder.Services.AddScoped<IFournisseurEventHandler, FournisseurEventHandler>();
builder.Services.AddHostedService<FournisseurEventConsumer>();

builder.Services.AddScoped<IInvoiceCacheService, InvoiceCacheService>();
builder.Services.AddScoped<IInvoiceEventHandler, InvoiceEventHandler>();
builder.Services.AddHostedService<InvoiceEventConsumer>();

builder.Services.AddScoped<IInvoiceBonSortieMappingRepository, InvoiceBonSortieMappingRepository>();
// =========================
// CONTROLLERS & API
// =========================

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

        InvoiceTopics.Created, InvoiceTopics.Cancelled,
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

// =========================
// MIGRATIONS & SEEDING
// =========================
using (IServiceScope scope = app.Services.CreateScope())
{
    StockDbContext db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();

app.Run();