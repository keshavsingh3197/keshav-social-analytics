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

// Services
builder.Services.AddSingleton<AnalyticsEngineService>();
builder.Services.AddSingleton<RevenueCalculatorService>();
builder.Services.AddSingleton<GrowthAdvisorService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Native .NET OpenAPI
builder.Services.AddOpenApi();

// CORS for Keshav Singh ecosystem
const string CorsPolicy = "SocialAnalyticsCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            try
            {
                var uri = new Uri(origin);
                // Allow any keshavsingh.in domain, github.io, or onrender.com
                if (uri.Host.EndsWith("keshavsingh.in", StringComparison.OrdinalIgnoreCase)) return true;
                if (uri.Host.EndsWith("github.io", StringComparison.OrdinalIgnoreCase)) return true;
                if (uri.Host.EndsWith("onrender.com", StringComparison.OrdinalIgnoreCase)) return true;
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
            ClockSkew = TimeSpan.FromSeconds(30)
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
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "SocialAnalytics.Api",
    version = "1.0.0",
    time = DateTime.UtcNow
}));

app.MapControllers();

app.Run();
