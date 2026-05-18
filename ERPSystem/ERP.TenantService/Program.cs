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
using Microsoft.AspNetCore.RateLimiting;
using NSwag;
using NSwag.Generation.Processors.Security;

var builder = WebApplication.CreateBuilder(args);

//Database
builder.Services.AddDbContext<TenantDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//Repositories
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
builder.Services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();

//Application Services
builder.Services.AddScoped<ITenantService, ERP.TenantService.Application.Services.TenantService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

//Rate Limiting(protect from spam) 
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("OnboardingLimit", config =>
    {
        config.PermitLimit = 3;                          // 3 attempts
        config.Window = TimeSpan.FromMinutes(10);        // per 10 minutes for ip
        config.QueueLimit = 0;
    });
    options.RejectionStatusCode = 429; //Many Requests
});

//API Key Policy
builder.Services.AddSingleton<IAuthorizationHandler, ApiKeyAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiKeyPolicy", policy =>
        policy.AddRequirements(new ApiKeyRequirement()));
});

//Controllers
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

    // Add API Key security definition
    config.AddSecurity("ApiKey", new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.ApiKey,
        Name = "X-Api-Key",
        In = OpenApiSecurityApiKeyLocation.Header,
        Description = "Enter your API key in the field below"
    });

    config.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("ApiKey"));
});

//Build
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
    await SubscriptionPlanSeeder.SeedAsync(db);
}

// Middleware Pipeline 
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
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();