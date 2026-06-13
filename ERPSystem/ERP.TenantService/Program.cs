using Confluent.Kafka;
using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Application.Interfaces.Repositories;
using ERP.TenantService.Application.Interfaces.Services;
using ERP.TenantService.Application.Services;
using ERP.TenantService.Infrastructure.Messaging;
using ERP.TenantService.Infrastructure.Persistence;
using ERP.TenantService.Infrastructure.Persistence.Repositories;
using ERP.TenantService.Infrastructure.Persistence.Seeders;
using ERP.TenantService.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' is not configured.");

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

builder.Services.AddDbContext<TenantDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;

    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
})
.ConfigureApiBehaviorOptions(options =>
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
builder.Services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();

builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();

builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddHostedService<KafkaTopicInitializer>();

builder.Services.AddHostedService<SubscriptionExpiryJob>();

WebApplication app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

    await db.Database.MigrateAsync();
    await SubscriptionPlanSeeder.SeedAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRouting();
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