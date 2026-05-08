using ERP.AuthService.Application.Interfaces;
using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Application.Services;
using ERP.AuthService.Domain;
using ERP.AuthService.Infrastructure.Configuration;
using ERP.AuthService.Infrastructure.Http;
using ERP.AuthService.Infrastructure.Persistence;
using ERP.AuthService.Infrastructure.Persistence.Repositories;
using ERP.AuthService.Infrastructure.Security;
using ERP.AuthService.Middleware;
using ERPrivileges.AuthService.Infrastructure.Persistence.Seeder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Text;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── Add environment variables
builder.Configuration.AddEnvironmentVariables();

// ── Controllers & Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Enum Serializing (0 => "string")
BsonSerializer.RegisterSerializer(new EnumSerializer<Theme>(BsonType.String));
BsonSerializer.RegisterSerializer(new EnumSerializer<Language>(BsonType.String));

// ── Mongo GUID Serializer
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// ── Mongo Configuration
builder.Services
    .AddOptions<MongoSettings>()
    .Bind(builder.Configuration.GetSection("MongoSettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<MongoDbContext>(sp =>
{
    MongoSettings settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
    return new MongoDbContext(settings.ConnectionString, settings.DatabaseName);
});

// ── JWT Settings
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JWT"));

// ── JWT Parsing (no validation, gateway already did it)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string authHeader = context.Request.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Bearer "))
                    context.Token = authHeader["Bearer ".Length..].Trim();
                return Task.CompletedTask;
            }
        };

        byte[] key = Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"] ?? throw new Exception("JWT:Secret not found"));
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JWT:Issuer"] ?? throw new Exception("JWT:Issuer not found"),

            ValidateAudience = true,
            ValidAudience = builder.Configuration["JWT:Audience"] ?? throw new Exception("JWT:Audience not found"),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),

            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

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
            code = "VALIDATION ERROR",
            message
        });
    };
});

// ── Dependency Injection
builder.Services.AddScoped<IAuthUserRepository, AuthUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IControleRepository, ControleRepository>();
builder.Services.AddScoped<IPrivilegeRepository, PrivilegeRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthUserService, AuthUserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IControleService, ControleService>();
builder.Services.AddScoped<IPrivilegeService, PrivilegeService>();
builder.Services.AddScoped<IPasswordHasher<AuthUser>, PasswordHasher<AuthUser>>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddHttpContextAccessor();

// ── Tenant Service Client (must be before Build())
builder.Services.AddHttpClient<ITenantServiceClient, TenantServiceClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5098/");
});

// =========================
// BUILD
// =========================
WebApplication app = builder.Build();

// ── Seed data
using (IServiceScope scope = app.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;

    MongoDbContext dbContext = services.GetRequiredService<MongoDbContext>();

    // ── Initialize MongoDB indexes
    await MongoDbInitializer.InitializeAsync(dbContext);

    // ── Seed data
    await AuthServiceSeeder.SeedAsync(
        dbContext,
        services.GetRequiredService<IAuditLogRepository>(),
        services.GetRequiredService<IAuthUserRepository>(),
        services.GetRequiredService<IRoleRepository>(),
        services.GetRequiredService<IControleRepository>(),
        services.GetRequiredService<IPrivilegeRepository>(),
        services.GetRequiredService<IPasswordHasher<AuthUser>>(),
        services.GetRequiredService<IConfiguration>()
    );
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

app.UseMiddleware<ValidateUserMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();

app.Run();