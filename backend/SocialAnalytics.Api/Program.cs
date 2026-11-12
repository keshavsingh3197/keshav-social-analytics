using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SocialAnalytics.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms inject the listen port via $PORT
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Persistence + Dynamic Domain Services
builder.Services.AddSingleton<HybridDataStore>();
builder.Services.AddSingleton<SocialConfigService>();
builder.Services.AddSingleton<FunnelService>();
builder.Services.AddSingleton<AnalyticsEngineService>();
builder.Services.AddSingleton<RevenueCalculatorService>();
builder.Services.AddSingleton<GrowthAdvisorService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Native .NET OpenAPI
builder.Services.AddOpenApi();

// CORS for Keshav Singh ecosystem + configured AllowedOrigins
const string CorsPolicy = "SocialAnalyticsCors";
var configuredOrigins = (builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
    .Select(o => o.Trim().TrimEnd('/'))
    .Where(o => !string.IsNullOrEmpty(o))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            var normalized = origin.Trim().TrimEnd('/');
            if (configuredOrigins.Contains(normalized)) return true;
            try
            {
                var uri = new Uri(normalized);
                if (uri.Host.Equals("keshavsingh.in", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith(".keshavsingh.in", StringComparison.OrdinalIgnoreCase)) return true;
                if (uri.Host.Equals("keshavsingh3197.github.io", StringComparison.OrdinalIgnoreCase)) return true;
                if (uri.Host.Equals("keshav-social-analytics.onrender.com", StringComparison.OrdinalIgnoreCase)) return true;
                if (builder.Environment.IsDevelopment() && (uri.Host == "localhost" || uri.Host == "127.0.0.1")) return true;
                return false;
            }
            catch
            {
                return false;
            }
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Auth (JWT validation trusting admin.keshavsingh.in / id.keshavsingh.in IdP)
var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"] ?? "keshavsingh-idp";
var audience = jwtSection["Audience"] ?? "keshavsingh-apps";
var signingKey = jwtSection["SigningKey"] ?? "dev_signing_key_for_local_keshavsingh_ecosystem_32chars!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                signingKey.Length >= 32 ? signingKey : signingKey.PadRight(32, '0'))),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Normalize short "role" claims to ClaimTypes.Role if issued without full schema URI.
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var shortRoles = identity.FindAll("role").Select(c => c.Value).ToList();
                    foreach (var role in shortRoles)
                    {
                        if (!identity.HasClaim(ClaimTypes.Role, role))
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// Dedicated health endpoint for Render health checks
app.MapGet("/health", (HybridDataStore store) => Results.Ok(new
{
    status = "Healthy",
    service = "SocialAnalytics.Api",
    version = "2.0.0",
    storage = store.StorageMode,
    time = DateTime.UtcNow
}));

// Direct server-side tracked redirect endpoint for short links shared externally
app.MapGet("/t/{slug}", async (string slug, FunnelService funnelService) =>
{
    var tracked = await funnelService.TrackClickAsync(slug);
    return tracked is not null
        ? Results.Redirect(tracked.DestinationUrl)
        : Results.NotFound(new { message = "Tracking campaign not found or inactive." });
});

app.MapControllers();

app.Run();
