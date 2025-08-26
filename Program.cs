using Imagino.Api.DependencyInjection;
using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Carregar user-secrets em desenvolvimento
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Configurações
builder.Services.Configure<ImageGeneratorSettings>(builder.Configuration.GetSection("ImageGeneratorSettings"));
builder.Services.Configure<ReplicateSettings>(builder.Configuration.GetSection("ReplicateSettings"));
builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("Frontend"));

builder.Services.AddSingleton<ImageJobRepository>();
builder.Services.AddScoped<WebhookImageService>();

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetSection("ImageGeneratorSettings")["MongoConnection"];
    return new MongoClient(conn);
});

// Configuração de CORS
var corsPolicyName = "AllowFrontend";

// Lê lista de origens permitidas (array) via envs: Cors__AllowedOrigins__0, __1, ...
var allowedPatterns = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

// Fallback: se vier vazio, usa Frontend:BaseUrl (caso você ainda use isso em algum lugar)
if (allowedPatterns.Length == 0)
{
    var fb = builder.Configuration["Frontend:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(fb))
        allowedPatterns = new[] { fb };
}

// Log para facilitar debug no Render
Console.WriteLine("CORS AllowedOrigins => " + string.Join(", ", allowedPatterns));

// Função de match por host com suporte a wildcard "*."
static bool OriginMatches(string? origin, string[] patterns)
{
    if (string.IsNullOrEmpty(origin)) return false;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    var host = uri.Host.ToLowerInvariant();

    foreach (var raw in patterns)
    {
        if (string.IsNullOrWhiteSpace(raw)) continue;

        var p = raw.Trim().ToLowerInvariant()
                   .Replace("https://", "")
                   .Replace("http://", "")
                   .TrimEnd('/');

        if (p.StartsWith("*.")) // ex: *.vercel.app
        {
            var suffix = p.Substring(2); // "vercel.app"
            if (host == suffix || host.EndsWith("." + suffix)) return true;
        }
        else
        {
            if (host == p) return true; // origem exata, ex: imagino-front.vercel.app
        }
    }
    return false;
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy
            .SetIsOriginAllowed(origin => OriginMatches(origin, allowedPatterns))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


// Porta configurável (para Render)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseKestrel()
    .UseUrls($"http://0.0.0.0:{port}");

// Adicionar serviços do projeto
builder.Services.AddAppServices(builder.Configuration);

// Controllers, Swagger, Endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Sub;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Swagger e HTTPS só no desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

// Middleware pipeline
app.UseStaticFiles();
app.UseRouting();
app.UseCors(corsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
