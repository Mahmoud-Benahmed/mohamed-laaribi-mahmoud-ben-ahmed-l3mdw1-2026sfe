using ERP.Gateway.Cache;
using ERP.Gateway.Infrastructure.BackgroundServices;
using ERP.Gateway.Infrastructure.Messaging;
using ERP.Gateway.Middleware;
using ERP.Gateway.Properties;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

ConfigurationManager config = builder.Configuration;
string GetClusterAddress(string clusterName, string destinationName)
{
    var address = config[
        $"ReverseProxy:Clusters:{clusterName}:Destinations:{destinationName}:Address"]
        ?? throw new InvalidOperationException(
            $"Cluster '{clusterName}' destination '{destinationName}' not found in ReverseProxy configuration.");

    return address.TrimEnd('/');
}


/////////////////////////////////////////////////////////////
// Health Checks
/////////////////////////////////////////////////////////////
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());
builder.Services.AddHttpContextAccessor();

var jwtSecret = config["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is not configured.");
var jwtIssuer = config["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is not configured.");
var jwtAudience = config["JWT:Audience"] ?? throw new InvalidOperationException("JWT:Audience is not configured.");

builder.Services.AddHttpClient<ITenantDirectoryClient, TenantDirectoryClient>(
    client =>
    {
        client.BaseAddress = new Uri(GetClusterAddress("tenantCluster", "tenantDestination"));

        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
    signingKey.KeyId = "erp-key-1";

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = signingKey,

        RoleClaimType = "role",
        NameClaimType = "login",

        ClockSkew = TimeSpan.FromMinutes(5),
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context => Task.CompletedTask,

        // ✅ ONLY validates consistency — does NOT resolve tenant
        OnTokenValidated = context =>
        {
            var jwtTenantId = context.Principal?.FindFirstValue("tenantId");
            var resolvedTenantId = context.HttpContext.Items["tenantId"]?.ToString();

            // Both are now strings — comparison is safe
            if (!string.IsNullOrEmpty(jwtTenantId) &&
                !string.IsNullOrEmpty(resolvedTenantId) &&
                !string.Equals(jwtTenantId, resolvedTenantId, StringComparison.OrdinalIgnoreCase))
            {
                context.Fail("Tenant mismatch: JWT tenantId does not match the resolved tenant.");
            }

            return Task.CompletedTask;
        },

        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 401,
                code = "AUTH_006",
                message = "Authentication required. Please log in."
            });
        },

        OnForbidden = async context =>
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 403,
                code = "AUTH_007",
                message = "You do not have permission to access this resource."
            });
        }
    };
});
builder.Services.AddHostedService<KafkaTopicInitializer>();
builder.Services.AddHostedService<TenantLifecycleConsumer>(); 
builder.Services.AddHostedService<TenantCacheWarmupService>();

//////////////////////////////////////////////////
// Authorization
//////////////////////////////////////////////////

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    void Add(string policy, params string[] privileges) =>
        options.AddPolicy(policy, p =>
            p.RequireAuthenticatedUser()
             .RequireAssertion(ctx =>
                 privileges.Any(r => ctx.User.HasClaim("privilege", r))));

    options.AddPolicy("JwtPolicy", p => p.RequireAuthenticatedUser());

    options.AddPolicy("AdminOnly", p =>
        p.RequireAuthenticatedUser()
         .RequireRole(Roles.SystemAdmin));

    // ── Individual tenant privileges (platform-scoped)
    Add(TenantPrivileges.VIEW_TENANTS, TenantPrivileges.VIEW_TENANTS);
    Add(TenantPrivileges.CREATE_TENANT, TenantPrivileges.CREATE_TENANT);
    Add(TenantPrivileges.UPDATE_TENANT, TenantPrivileges.UPDATE_TENANT);
    Add(TenantPrivileges.DELETE_TENANT, TenantPrivileges.DELETE_TENANT);
    Add(TenantPrivileges.SUSPEND_TENANT, TenantPrivileges.SUSPEND_TENANT);
    Add(TenantPrivileges.ACTIVATE_TENANT, TenantPrivileges.ACTIVATE_TENANT);
    Add(TenantPrivileges.MANAGE_SUBSCRIPTIONS, TenantPrivileges.MANAGE_SUBSCRIPTIONS);
    Add(TenantPrivileges.VIEW_BILLING, TenantPrivileges.VIEW_BILLING);

    // ── Individual privilege policies (tenant-scoped, from registry)
    foreach (PrivilegeDefinition privilege in PrivilegeRegistry.All)
        Add(privilege.Code, privilege.Code);

    // ── Composite policies
    Add("ASSIGN_SUBSCRIPTION_OR_BUY",
        TenantPrivileges.MANAGE_SUBSCRIPTIONS,
        Privileges.Users.BUY_SUBSCRIPTION); ;

    Add("EDIT_TENANT_SETTINGS", 
        Privileges.Users.EDIT_SYSTEM_SETTINGS, 
        TenantPrivileges.UPDATE_TENANT
    );

    Add(Privileges.Users.MANAGE_USERS,
        Privileges.Users.VIEW_USERS,
        Privileges.Users.CREATE_USER,
        Privileges.Users.UPDATE_USER,
        Privileges.Users.DELETE_USER,
        Privileges.Users.ACTIVATE_USER,
        Privileges.Users.DEACTIVATE_USER,
        Privileges.Users.RESTORE_USER,
        Privileges.Users.ASSIGN_ROLES);

    Add("MANAGE_ROLES",
        Privileges.Users.CREATE_ROLE,
        Privileges.Users.UPDATE_ROLE,
        Privileges.Users.DELETE_ROLE);

    Add("MANAGE_CONTROLES",
        Privileges.Users.CREATE_CONTROLE,
        Privileges.Users.UPDATE_CONTROLE,
        Privileges.Users.DELETE_CONTROLE);

    Add("MANAGE_CLIENTS",
        Privileges.Clients.VIEW_CLIENTS,
        Privileges.Clients.CREATE_CLIENT,
        Privileges.Clients.UPDATE_CLIENT,
        Privileges.Clients.DELETE_CLIENT,
        Privileges.Clients.RESTORE_CLIENT,
        Privileges.Clients.CREATE_CLIENT_CATEGORIES,
        Privileges.Clients.UPDATE_CLIENT_CATEGORIES,
        Privileges.Clients.DELETE_CLIENT_CATEGORIES,
        Privileges.Clients.RESTORE_CLIENT_CATEGORIES);

    Add("MANAGE_ARTICLES",
        Privileges.Articles.VIEW_ARTICLES,
        Privileges.Articles.CREATE_ARTICLE,
        Privileges.Articles.UPDATE_ARTICLE,
        Privileges.Articles.DELETE_ARTICLE,
        Privileges.Articles.RESTORE_ARTICLE,
        Privileges.Articles.CREATE_ARTICLE_CATEGORIES,
        Privileges.Articles.UPDATE_ARTICLE_CATEGORIES,
        Privileges.Articles.DELETE_ARTICLE_CATEGORIES,
        Privileges.Articles.RESTORE_ARTICLE_CATEGORIES);

    Add("MANAGE_INVOICES",
        Privileges.Invoices.VIEW_INVOICES,
        Privileges.Invoices.CREATE_INVOICE,
        Privileges.Invoices.UPDATE_DRAFT_INVOICE,
        Privileges.Invoices.MARK_INVOICE_PAID,
        Privileges.Invoices.CANCEL_INVOICE,
        Privileges.Invoices.DELETE_INVOICE,
        Privileges.Invoices.RESTORE_INVOICE);

    Add("MANAGE_PAYMENTS",
        Privileges.Payments.VIEW_PAYMENTS,
        Privileges.Payments.RECORD_PAYMENT,
        Privileges.Payments.CANCEL_PAYMENT);

    Add("MANAGE_STOCK",
        Privileges.Stock.VIEW_STOCK,
        Privileges.Stock.UPDATE_STOCK,
        Privileges.Stock.ADD_ENTRY);

    Add("MANAGE_FOURNISSEURS",
        Privileges.Fournisseurs.VIEW_FOURNISSEURS,
        Privileges.Fournisseurs.CREATE_FOURNISSEUR,
        Privileges.Fournisseurs.UPDATE_FOURNISSEUR,
        Privileges.Fournisseurs.DELETE_FOURNISSEUR,
        Privileges.Fournisseurs.RESTORE_FOURNISSEUR);

    Add("MANAGE_REPORTS",
        Privileges.Reports.VIEW_REPORTS,
        Privileges.Reports.EXPORT_REPORTS);

    Add("MANAGE_CLIENTS_STOCK",
        Privileges.Clients.VIEW_CLIENTS,
        Privileges.Stock.VIEW_STOCK,
        Privileges.Stock.ADD_ENTRY,
        Privileges.Stock.UPDATE_STOCK);

    Add("MANAGE_AUDITLOGS", Privileges.Audit.MANAGE_AUDITLOGS);
});

//////////////////////////////////////////////////
// Rate Limiting
//////////////////////////////////////////////////

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("LoginPolicy", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userId = context.User?.FindFirst("sub")?.Value;
        var login = context.Request.Headers["X-Login"].FirstOrDefault();
        var identity = userId ?? login ?? ip;

        var userAgent = context.Request.Headers.UserAgent.ToString();
        var userAgentHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(userAgent))
        );

        var key = $"rl:{identity}:{userAgentHash}";

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            });
    });

    options.AddPolicy("UserPolicy", context =>
    {
        context.Items["RateLimitPolicyName"] = "UserPolicy";
        string? userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("sub")?.Value
            : context.Connection.RemoteIpAddress?.ToString();

        return RateLimitPartition.GetSlidingWindowLimiter(
            userId ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            });
    });

    options.AddPolicy("WritePolicy", context =>
    {
        context.Items["RateLimitPolicyName"] = "WritePolicy";
        string? userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("sub")?.Value
            : context.Connection.RemoteIpAddress?.ToString();

        return RateLimitPartition.GetFixedWindowLimiter(
            userId ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = 429;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.ContentType = "application/json";

        string? policyName = context.HttpContext.Items["RateLimitPolicyName"]?.ToString();
        int retrySeconds = policyName switch
        {
            "LoginPolicy" => 5 * 60,
            _ => 60
        };

        context.HttpContext.Response.Headers.RetryAfter = retrySeconds.ToString();

        string FormatWaitTime(int s) => s >= 60
            ? $"{s / 60} minute{(s / 60 > 1 ? "s" : "")}"
            : $"{s} second{(s > 1 ? "s" : "")}";

        string message = policyName switch
        {
            "LoginPolicy" => $"Too many login attempts. Please wait {FormatWaitTime(retrySeconds)} before retrying.",
            "WritePolicy" => $"Too many write operations. Please wait {FormatWaitTime(retrySeconds)} before retrying.",
            "UserPolicy" => $"Request limit reached. Please wait {FormatWaitTime(retrySeconds)} before retrying.",
            _ => $"Too many requests. Please wait {FormatWaitTime(retrySeconds)} before retrying."
        };

        await context.HttpContext.Response.WriteAsync(
            $$"""{"statusCode":429,"code":"RATE_LIMIT","message":"{{message}}","retryAfterSeconds":{{retrySeconds}}}""");
    };
});

//////////////////////////////////////////////////
// CORS
//////////////////////////////////////////////////

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

//////////////////////////////////////////////////
// YARP
//////////////////////////////////////////////////

builder.Services.AddReverseProxy()
    .LoadFromConfig(config.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(async transformContext =>
        {
            ClaimsPrincipal user = transformContext.HttpContext.User;

            string? sub = user.FindFirstValue("sub");
            if (!string.IsNullOrEmpty(sub))
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-User-Id", sub);

            string? role = user.FindFirstValue("role");
            if (!string.IsNullOrEmpty(role))
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-User-Role", role);

            await Task.CompletedTask;
        });
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration =
        builder.Configuration.GetConnectionString("REDIS");

    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddSingleton<ITenantCache, RedisTenantCache>();

//////////////////////////////////////////////////
// Pipeline
//////////////////////////////////////////////////

WebApplication app = builder.Build();

app.UseCors("AllowFrontend");
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<RequestLoggingMiddleware>();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (HttpRequestException)
    {
        context.Response.StatusCode = 503;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = 503,
            code = "TENANT_SERVICE_UNAVAILABLE",
            message = "Tenant service is unavailable."
        });
    }
    catch (TaskCanceledException)
    {
        context.Response.StatusCode = 503;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = 503,
            code = "TENANT_SERVICE_TIMEOUT",
            message = "Tenant service request timed out."
        });
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = 500,
            code = "INTERNAL_ERROR",
            message = $"{ex.Message}"
            //message = "An unexpected error occurred."
        });
    }
});

app.UseRateLimiter();

// ✅ STEP 4: Authentication — OnTokenValidated reads Items["tenantId"] set above
app.UseAuthentication();

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();

// ✅ STEP 6: YARP — transform reads Items["tenantId"], no re-resolution
app.MapReverseProxy();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Name != "self"
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Name == "self"
});

app.Run();