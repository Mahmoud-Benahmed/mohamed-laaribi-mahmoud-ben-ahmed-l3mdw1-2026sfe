using ERP.Gateway.Middleware;
using ERP.Gateway.Middleware.ApiKeyAuthentication;
using ERP.Gateway.Properties;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

ConfigurationManager config = builder.Configuration;

// HELPERS — these can be moved to separate classes/files as needed
static string? ExtractSlug(string host)
{
    var parts = host.Split('.');
    if (parts.Length >= 3) return parts[0].ToLowerInvariant();
    return null; // handle local dev separately
}

/////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////

///////////////////////////////////////////////////
// Health Checks
///////////////////////////////////////////////////
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());
builder.Services.AddHttpContextAccessor();

var jwtSecret = config["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is not configured.");
var jwtIssuer = config["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is not configured.");
var jwtAudience = config["JWT:Audience"] ?? throw new InvalidOperationException("JWT:Audience is not configured.");

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
        ClockSkew = TimeSpan.FromMinutes(5),
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context => Task.CompletedTask,

        OnTokenValidated = context =>
        {
            var user = context.Principal;
            var identity = user?.Identity as ClaimsIdentity;
            if (identity == null) return Task.CompletedTask;

            // Ensure tenantId is present
            var tenantId = user.FindFirstValue("tenantId");
            if (!string.IsNullOrEmpty(tenantId))
            {
                context.HttpContext.Items["tenantId"] = tenantId;
            }
            else
            {
                // Fallback to slug-derived tenantId if set earlier
                if (context.HttpContext.Items["tenantId"] is string slugTenantId)
                {
                    context.HttpContext.Items["tenantId"] = slugTenantId;
                }
                else
                {
                    context.Fail("tenantId missing in token and request");
                    return Task.CompletedTask;
                }
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

//////////////////////////////////////////////////
// Authorization
//////////////////////////////////////////////////

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("JwtPolicy", p => p.RequireAuthenticatedUser()); // ← single declaration

    options.AddPolicy("AdminOnly", p =>
        p.RequireAuthenticatedUser()
         .RequireRole(Roles.SystemAdmin));

    // ── Individual privilege policies ──────────────────────────────────────
    foreach (PrivilegeDefinition privilege in PrivilegeRegistry.All)
    {
        options.AddPolicy(privilege.Code, p =>
            p.RequireAuthenticatedUser()
             .RequireClaim("privilege", privilege.Code));
    }

    // ── MANAGE composite policies ──────────────────────────────────────────
    void AddManagePolicy(string manageCode, params string[] related)
    {
        options.AddPolicy(manageCode, p =>
            p.RequireAuthenticatedUser()
             .RequireAssertion(ctx =>
                 related.Any(r => ctx.User.HasClaim("privilege", r))));
    }

    AddManagePolicy(Privileges.Users.MANAGE_USERS,
        Privileges.Users.VIEW_USERS,
        Privileges.Users.CREATE_USER,
        Privileges.Users.UPDATE_USER,
        Privileges.Users.DELETE_USER,
        Privileges.Users.ACTIVATE_USER,
        Privileges.Users.DEACTIVATE_USER,
        Privileges.Users.RESTORE_USER,
        Privileges.Users.ASSIGN_ROLES);

    AddManagePolicy("MANAGE_ROLES",
        Privileges.Users.CREATE_ROLE,
        Privileges.Users.UPDATE_ROLE,
        Privileges.Users.DELETE_ROLE);

    AddManagePolicy("MANAGE_CONTROLES",
        Privileges.Users.CREATE_CONTROLE,
        Privileges.Users.UPDATE_CONTROLE,
        Privileges.Users.DELETE_CONTROLE);

    AddManagePolicy("MANAGE_CLIENTS",
        Privileges.Clients.VIEW_CLIENTS,
        Privileges.Clients.CREATE_CLIENT,
        Privileges.Clients.UPDATE_CLIENT,
        Privileges.Clients.DELETE_CLIENT,
        Privileges.Clients.RESTORE_CLIENT,
        Privileges.Clients.CREATE_CLIENT_CATEGORIES,
        Privileges.Clients.UPDATE_CLIENT_CATEGORIES,
        Privileges.Clients.DELETE_CLIENT_CATEGORIES,
        Privileges.Clients.RESTORE_CLIENT_CATEGORIES);

    AddManagePolicy("MANAGE_ARTICLES",
        Privileges.Articles.VIEW_ARTICLES,
        Privileges.Articles.CREATE_ARTICLE,
        Privileges.Articles.UPDATE_ARTICLE,
        Privileges.Articles.DELETE_ARTICLE,
        Privileges.Articles.RESTORE_ARTICLE,
        Privileges.Articles.CREATE_ARTICLE_CATEGORIES,
        Privileges.Articles.UPDATE_ARTICLE_CATEGORIES,
        Privileges.Articles.DELETE_ARTICLE_CATEGORIES,
        Privileges.Articles.RESTORE_ARTICLE_CATEGORIES);

    AddManagePolicy("MANAGE_INVOICES",
        Privileges.Invoices.VIEW_INVOICES,
        Privileges.Invoices.CREATE_INVOICE,
        Privileges.Invoices.UPDATE_DRAFT_INVOICE,
        Privileges.Invoices.MARK_INVOICE_PAID,
        Privileges.Invoices.CANCEL_INVOICE,
        Privileges.Invoices.DELETE_INVOICE,
        Privileges.Invoices.RESTORE_INVOICE);

    AddManagePolicy("MANAGE_PAYMENTS",
        Privileges.Payments.VIEW_PAYMENTS,
        Privileges.Payments.RECORD_PAYMENT,
        Privileges.Payments.CANCEL_PAYMENT);

    AddManagePolicy("MANAGE_STOCK",
        Privileges.Stock.VIEW_STOCK,
        Privileges.Stock.UPDATE_STOCK,
        Privileges.Stock.ADD_ENTRY);

    AddManagePolicy("MANAGE_FOURNISSEURS",
        Privileges.Fournisseurs.VIEW_FOURNISSEURS,
        Privileges.Fournisseurs.CREATE_FOURNISSEUR,
        Privileges.Fournisseurs.UPDATE_FOURNISSEUR,
        Privileges.Fournisseurs.DELETE_FOURNISSEUR,
        Privileges.Fournisseurs.RESTORE_FOURNISSEUR);

    AddManagePolicy("MANAGE_REPORTS",
        Privileges.Reports.VIEW_REPORTS,
        Privileges.Reports.EXPORT_REPORTS);

    options.AddPolicy("MANAGE_AUDITLOGS", p =>
        p.RequireAuthenticatedUser()
         .RequireClaim("privilege", Privileges.Audit.MANAGE_AUDITLOGS));

    options.AddPolicy("MANAGE_CLIENTS_STOCK", p =>
    p.RequireAuthenticatedUser()
     .RequireAssertion(context =>
         context.User.Claims.Any(c =>
             c.Type == "privilege" &&
             (c.Value == Privileges.Clients.VIEW_CLIENTS ||
             c.Value == Privileges.Stock.VIEW_STOCK ||
              c.Value == Privileges.Stock.ADD_ENTRY ||
              c.Value == Privileges.Stock.UPDATE_STOCK))));

    options.AddPolicy("ApiKeyPolicy", policy =>
    {
        policy.AuthenticationSchemes.Add("ApiKey");
        policy.RequireAuthenticatedUser();
    });
});

//////////////////////////////////////////////////
// Rate Limiting  — unchanged
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
// CORS  — unchanged
//////////////////////////////////////////////////

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

//////////////////////////////////////////////////
// YARP  — unchanged
//////////////////////////////////////////////////

builder.Services.AddReverseProxy()
    .LoadFromConfig(config.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(async transformContext =>
        {
            ClaimsPrincipal user = transformContext.HttpContext.User;

            // For authenticated requests: use JWT tenantId
            string? tenantId = user.FindFirstValue("tenantId");

            // For pre-auth requests (login): use slug-derived tenantId
            if (string.IsNullOrEmpty(tenantId))
            {
                var slug = transformContext.HttpContext.Items["TenantSlug"]?.ToString();
                if (!string.IsNullOrEmpty(slug))
                {
                    // TODO: resolve slug to tenantId via cache
                    // For now, pass slug through
                    tenantId = slug; // temporary - replace with actual lookup
                }
            }

            // Add tenantId header if we have it
            if (!string.IsNullOrEmpty(tenantId))
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-Tenant-Id", tenantId);

            // Optionally add user info headers for downstream services
            string? sub = user.FindFirstValue("sub");
            if (!string.IsNullOrEmpty(sub))
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-User-Id", sub);

            // Role is optional and may not be present in all tokens, but if it is, pass it along
            string? role = user.FindFirstValue("role");
            if (!string.IsNullOrEmpty(role))
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-User-Role", role);
        });
    });

builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();

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

app.Use(async (context, next) =>
{
    // Extract slug from Host header for pre-auth requests
    var host = context.Request.Host.Host;
    var slug = ExtractSlug(host);

    if (!string.IsNullOrEmpty(slug))
    {
        context.Items["TenantSlug"] = slug;
        context.Request.Headers["X-Tenant-Slug"] = slug;
        if (!context.Items.ContainsKey("tenantId"))
        {
            context.Items["tenantId"] = slug; // temporary
        }
    }

    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name != "self"
});

app.Use(async (context, next) =>
{
    var userId = context.User.FindFirst("sub")?.Value ?? "anonymous";
    var start = DateTime.UtcNow;
    var logger= context.RequestServices.GetRequiredService<ILogger<Program>>();
    await next();

    logger.LogInformation(
        "Request {Path} {Method} {Status} {Duration}ms User:{UserId}",
        context.Request.Path, context.Request.Method,
        context.Response.StatusCode,
        DateTime.UtcNow - start, userId);
});

app.Run();