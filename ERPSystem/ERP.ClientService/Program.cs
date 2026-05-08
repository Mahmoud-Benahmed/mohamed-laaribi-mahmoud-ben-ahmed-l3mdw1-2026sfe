using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Application.Services;
using ERP.ClientService.Infrastructure.Messaging;
using ERP.ClientService.Infrastructure.Persistence;
using ERP.ClientService.Infrastructure.Persistence.Repositories;
using ERP.ClientService.Infrastructure.Persistence.Seeders;
using ERP.ClientService.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// =========================
// DATABASE
// =========================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantConnectionString>();

builder.Services.AddDbContext<ClientDbContext>((serviceProvider, options) =>
{
    var tenantConnectionString = serviceProvider.GetRequiredService<TenantConnectionString>();
    options.UseSqlServer(tenantConnectionString.Resolve());
});

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

builder.Services.AddHostedService<TenantProvisioningConsumer>();

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
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    }).ConfigureApiBehaviorOptions(options =>
    {
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

    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();
    await seeder.SeedAsync();
}

// =========================
// PIPELINE
// =========================
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<TenantResolver>();
app.MapControllers();

app.Run();