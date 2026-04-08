using Capstone.Api.Data;
using Capstone.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuration sources, in order of precedence (later wins):
//   1. appsettings.json                — committed defaults / placeholders
//   2. appsettings.{Environment}.json  — committed env-specific overrides
//   3. appsettings.Local.json          — gitignored, local dev secrets
//   4. Environment variables           — production secrets (e.g. Jwt__Key, ConnectionStrings__AzureSql)
//   5. Command-line arguments
// In production, NEVER ship appsettings.Local.json. Set secrets via environment variables
// (or Azure App Service "Application Settings", which become env vars at runtime).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Hard caps on request body size — defense against memory exhaustion / oversized payload DoS.
// Per-endpoint [RequestSizeLimit(...)] still overrides this for upload routes.
const long MaxRequestBodyBytes = 50L * 1024 * 1024; // 50 MB (matches the largest upload endpoint)
const int MaxJsonPayloadBytes = 1 * 1024 * 1024;    // 1 MB JSON cap for non-upload routes

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
    o.Limits.MaxRequestHeadersTotalSize = 32 * 1024;       // 32 KB
    o.Limits.MaxRequestLineSize = 8 * 1024;                // 8 KB
});

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxRequestBodyBytes;
    o.ValueLengthLimit = MaxJsonPayloadBytes;
    o.KeyLengthLimit = 1024;
});

builder.Services.AddControllers(o =>
{
    // Reject malformed payloads (e.g. fields of wrong type) with a clean 400 instead of crashing.
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = false;
})
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        // Cap deserialization depth to prevent stack-exhaustion via deeply nested JSON.
        o.JsonSerializerOptions.MaxDepth = 32;
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Tenurix API v1", Version = "v1" });

    //  Fixes Swagger 500 when DTO class names collide (very common)
    c.CustomSchemaIds(t => t.FullName);

    // (optional but recommended for JWT testing in Swagger)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS — restrict to known origins, methods, and headers only (H3)
builder.Services.AddCors(options =>
{
    options.AddPolicy("TenurixWeb", policy =>
    {
        policy
            .WithOrigins(
                "https://tenurix.net",
                "https://www.tenurix.net",
                "https://client.tenurix.net",
                "https://landlord.tenurix.net",
                "https://manage.tenurix.net",
                "http://localhost:3000"
            )
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization", "X-Health-Key");
    });
});

// Rate limiting — protect auth endpoints from brute force (H2)
// All limiters partition by client IP so one abusive caller cannot starve everyone else.
builder.Services.AddRateLimiter(options =>
{
    static string ClientKey(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

    // Auth routes (login, register, forgot/reset password): 5 attempts per IP per 15 minutes.
    options.AddPolicy("login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(15),
            PermitLimit = 5,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }));

    // 2FA code verification: 5 attempts per IP per 15 minutes.
    options.AddPolicy("verify2fa", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(15),
            PermitLimit = 5,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }));

    // Resend 2FA code: 3 per IP per 15 minutes (tighter — prevents email-bombing).
    options.AddPolicy("resend2fa", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(15),
            PermitLimit = 3,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }));

    // Global default for every other endpoint: 100 requests per IP per minute.
    // Catches scrapers and runaway clients without affecting normal interactive use.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 100,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }));

    // Return 429 as JSON with a Retry-After hint instead of plain text.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.ContentType = "application/json";
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        await ctx.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many requests. Please wait and try again.\"}", token);
    };
});

// DB + Services
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<TwoFactorService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AuditService>();

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key is missing or too short. Set a secure key (minimum 32 characters) in appsettings.Local.json.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? "Capstone.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? "Capstone.Clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Global exception handler — always returns JSON, never leaks internal details
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(new { message = "Something went wrong. Please try again later." });
        await ctx.Response.WriteAsync(json);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tenurix API v1");
        c.RoutePrefix = "swagger";
    });
}

// HSTS — tell browsers to always use HTTPS (H5)
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseRateLimiter();

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

app.UseStaticFiles();
app.UseCors("TenurixWeb");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
