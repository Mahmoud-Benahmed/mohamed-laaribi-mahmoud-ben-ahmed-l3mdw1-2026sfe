using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Application.Services;
using ERP.ArticleService.Infrastructure.Messaging;
using ERP.ArticleService.Infrastructure.Persistence;
using ERP.ArticleService.Infrastructure.Persistence.Seeders;
using ERP.ArticleService.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<ArticleDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
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

builder.Services.AddScoped<IArticleRepository, ArticleRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IArticleCodeRepository, ArticleCodeRepository>();

builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IArticleCodeService, ArticleCodeService>();

builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

builder.Services.AddScoped<ArticleCodeSeeder>();
builder.Services.AddScoped<CategorySeeder>();
builder.Services.AddScoped<ArticleSeeder>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;
    ILogger<Program> logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        ERP.ArticleService.Infrastructure.Persistence.ArticleDbContext context = services.GetRequiredService<ERP.ArticleService.Infrastructure.Persistence.ArticleDbContext>();

        logger.LogInformation("Applying migrations...");
        await context.Database.MigrateAsync();

        logger.LogInformation("Seeding article codes...");
        await services.GetRequiredService<ArticleCodeSeeder>().SeedAsync();

        logger.LogInformation("Seeding categories...");
        await services.GetRequiredService<CategorySeeder>().SeedAsync();

        logger.LogInformation("Seeding articles...");
        await services.GetRequiredService<ArticleSeeder>().SeedAsync();

        logger.LogInformation("All seeding completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
        throw;
    }
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