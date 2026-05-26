using Confluent.Kafka;
using ERP.FournisseurService.Application.Interfaces;
using ERP.FournisseurService.Application.Services;
using ERP.FournisseurService.Infrastructure.Messaging;
using ERP.FournisseurService.Infrastructure.Persistence;
using ERP.FournisseurService.Infrastructure.Persistence.Repositories;
using ERP.FournisseurService.Infrastructure.Persistence.Seeders;
using ERP.FournisseurService.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// =========================
// DATABASE
// =========================
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionString 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<FournisseurDbContext>(options =>
    options.UseSqlServer(connectionString));


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
// DEPENDENCY INJECTION
// =========================
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IFournisseurRepository, FournisseurRepository>();
builder.Services.AddScoped<IFournisseurService, FournisseurService>();
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

builder.Services.AddHostedService<KafkaTopicInitializer>();
builder.Services.AddHostedService<TenantLifecycleConsumer>();

builder.Services.AddScoped<FournisseurSeeder>();

builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

// =========================
// CONTROLLERS & API
// =========================
builder.Services
    .AddControllers(options =>
        options.SuppressAsyncSuffixInActionNames = false)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles; // ← add this
    }).ConfigureApiBehaviorOptions(options =>
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
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDatabaseSeeders();

// =========================
// BUILD
// =========================
WebApplication app = builder.Build();

// =========================
// AUTO MIGRATE & SEED
// =========================
using (IServiceScope scope = app.Services.CreateScope())
{
    FournisseurDbContext db = scope.ServiceProvider.GetRequiredService<FournisseurDbContext>();
    await db.Database.MigrateAsync();
}

// =========================
// PIPELINE
// =========================
if(app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
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