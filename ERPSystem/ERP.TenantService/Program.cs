using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Application.Services;
using ERP.TenantService.Middleware;
using ERP.TenantService.Infrastructure.Messaging;
using ERP.TenantService.Infrastructure.Persistence;
using ERP.TenantService.Infrastructure.Persistence.Repositories;
using ERP.TenantService.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NSwag.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TenantDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
builder.Services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();

builder.Services.AddScoped<ITenantService, ERP.TenantService.Application.Services.TenantService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

// API Key Policy
builder.Services.AddSingleton<IAuthorizationHandler, ApiKeyAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiKeyPolicy", policy =>
        policy.AddRequirements(new ApiKeyRequirement()));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "ERP TenantService API";
    config.Version = "ERP.TenantService";
    config.Description = "Tenant management microservice";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
    await SubscriptionPlanSeeder.SeedAsync(db);
}

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(settings =>
    {
        settings.DocumentTitle = "ERP TenantService API";
        settings.Path = "/swagger";
    });
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();