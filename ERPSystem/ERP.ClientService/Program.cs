using Confluent.Kafka;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Application.Services;
using ERP.ClientService.Infrastructure.Messaging;
using ERP.ClientService.Infrastructure.Persistence;
using ERP.ClientService.Infrastructure.Persistence.Repositories;
using ERP.ClientService.Infrastructure.Persistence.Seeders;
using ERP.ClientService.Middleware;
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

builder.Services.AddDbContext<ClientDbContext>(options =>
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
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

builder.Services.AddScoped<CategorySeeder>();
builder.Services.AddScoped<ClientSeeder>();

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
    ClientDbContext db = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
    DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

    if (app.Environment.IsDevelopment())
        await db.Database.EnsureDeletedAsync();

    await db.Database.MigrateAsync();
    await seeder.SeedAsync();
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