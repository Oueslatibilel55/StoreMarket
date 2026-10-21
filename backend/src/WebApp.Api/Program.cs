using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using WebApp.Api.Auth;
using WebApp.Api.Endpoints;
using WebApp.Application.DTOs;
using WebApp.Application.Extensions;
using WebApp.Infrastructure.Extensions;
using WebApp.Infrastructure.Persistence;
using WebApp.Infrastructure.Security;
using WebApp.Infrastructure.Storage;
using WebApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// -- Service defaults (Aspire: OpenTelemetry, health checks, resilience) -------
builder.AddServiceDefaults();

// -- Application & Infrastructure ---------------------------------------------
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// Uploads live in a folder (Storage:RootPath, default "uploads" next to the API), served below.
builder.Services.PostConfigure<StorageOptions>(o =>
    o.RootPath = Path.GetFullPath(o.RootPath, builder.Environment.ContentRootPath));

// -- Auth ----------------------------------------------------------------------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrEmpty(jwt.Secret))
    throw new InvalidOperationException(
        "Jwt:Secret is not configured. In development run: " +
        "dotnet user-secrets set \"Jwt:Secret\" <at least 32 chars> --project src/WebApp.Api. " +
        "In production set the Jwt__Secret environment variable.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = JwtTokenService.SigningKey(jwt),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthPolicies();

// -- Rate limiting -------------------------------------------------------------
// Credential endpoints are cheap to spam and expensive to verify (BCrypt), and the
// caller has no identity yet, so they partition on IP.
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            $"auth:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 10),
                SegmentsPerWindow = 6,
            }));
    // Code checks: no brute-forcing affiliate codes. Signed-in callers get their own bucket, visitors share their IP.
    opt.AddPolicy(RateLimitPolicies.AffiliateCode, context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.User.GetUserId() is { } userId
                ? $"code:user:{userId}"
                : $"code:ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = builder.Configuration.GetValue("RateLimiting:AffiliateCodePerMinute", 30),
                SegmentsPerWindow = 6,
            }));
    // Cart calls that check codes (add with a code, set a code, guest preview, merge): roomier than a single code
    // check, since a client edits their cart often, but still bounded.
    opt.AddPolicy(RateLimitPolicies.Cart, context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.User.GetUserId() is { } userId
                ? $"cart:user:{userId}"
                : $"cart:ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = builder.Configuration.GetValue("RateLimiting:CartPerMinute", 60),
                SegmentsPerWindow = 6,
            }));
});

// -- API -----------------------------------------------------------------------
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// -- CORS ----------------------------------------------------------------------
builder.Services.AddCors(opt =>
    opt.AddPolicy("angular", p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// -- Proxies (hosted: Netlify proxy + Render load balancer) -----------------------
builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opt.ForwardLimit = 2;
    opt.KnownIPNetworks.Clear();
    opt.KnownProxies.Clear();
});

var app = builder.Build();

// -- Database migrations ---------------------------------------------------------
// Opt-in (Database__MigrateOnStartup=true on a deployed service): locally migrations
// stay an explicit `dotnet ef database update`.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// -- Middleware ----------------------------------------------------------------
// Behind Netlify's proxy and Render's load balancer the client IP is in X-Forwarded-For: read it so the
// per-IP rate limits (auth, cart, codes) apply per visitor, not to everyone at once.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCors("angular");

// Uploaded images as static files: /uploads/product-photos/2026/10/{id}.webp. Names are unique
// ids, so a file never changes — browsers may cache it for good. nosniff: the browser must trust
// the content type (only checked image types are ever written), never guess one.
var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
if (storage.UseDatabase)
{
    // Same URLs, bytes read from the file_blobs table (ADR 0006).
    app.MapGet(ImageUrls.RequestPath + "/{**path}", async (string path, DatabaseFileStorage files, HttpContext http, CancellationToken ct) =>
    {
        if (await files.ReadAsync(path, ct) is not { } data) return Results.NotFound();
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Results.Bytes(data, ImageContentTypes.For(path));
    }).ExcludeFromDescription();
}
else
{
    Directory.CreateDirectory(storage.RootPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(storage.RootPath),
        RequestPath = ImageUrls.RequestPath,
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        },
    });
}
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// -- Endpoints -----------------------------------------------------------------
app.MapDefaultEndpoints(); // /health, /alive
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapRoleRequestEndpoints();
app.MapAdminUserEndpoints();
app.MapSettingsEndpoints();
app.MapStoreCategoryEndpoints();
app.MapImageEndpoints();
app.MapStoreEndpoints();
app.MapProductCategoryEndpoints();
app.MapProductEndpoints();
app.MapShopEndpoints();
app.MapModerationEndpoints();
app.MapPartnershipEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();
app.MapOrderLifecycleEndpoints();
app.MapCoinEndpoints();
app.MapPayoutEndpoints();

await app.RunAsync();

public partial class Program;

/// <summary>Content type of a stored image from its extension (only checked JPEG / PNG / WebP are ever written).</summary>
internal static class ImageContentTypes
{
    public static string For(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".webp" => "image/webp",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
}
