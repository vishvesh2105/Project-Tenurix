using Capstone.Api.Data;
using Capstone.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Load local secrets file (gitignored) if present — overrides appsettings.json placeholders
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
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
builder.Services.AddRateLimiter(options =>
{
    // Login: max 10 attempts per minute per IP
    options.AddFixedWindowLimiter("login", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;
        o.QueueLimit = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // 2FA verify: max 5 attempts per minute per IP
    options.AddFixedWindowLimiter("verify2fa", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 5;
        o.QueueLimit = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Resend 2FA: max 3 per minute per IP
    options.AddFixedWindowLimiter("resend2fa", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 3;
        o.QueueLimit = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Return 429 as JSON instead of plain text
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.ContentType = "application/json";
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
            ClockSkew = TimeSpan.FromSeconds(30)
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
