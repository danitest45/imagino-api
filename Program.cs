using Imagino.Api.DependencyInjection;
using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Settings;
using Imagino.Api.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
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
builder.Services.Configure<RefreshTokenCookieSettings>(builder.Configuration.GetSection("RefreshTokenCookie"));
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

var stripeConfig = builder.Configuration.GetSection("Stripe").Get<StripeSettings>();
Stripe.StripeConfiguration.ApiKey = stripeConfig.ApiKey;

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

var allowedPatterns = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (allowedPatterns.Length == 0)
{
    var fb = builder.Configuration["Frontend:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(fb))
        allowedPatterns = new[] { fb };
}

Console.WriteLine("CORS AllowedOrigins => " + string.Join(", ", allowedPatterns));

static bool OriginMatches(string? origin, string[] patterns)
{
    if (string.IsNullOrEmpty(origin)) return false;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

    var host = uri.Host.ToLowerInvariant();
    var port = uri.IsDefaultPort ? (int?)null : uri.Port;

    foreach (var raw in patterns)
    {
        if (string.IsNullOrWhiteSpace(raw)) continue;

        // normaliza padrão: remove esquema, e separa host[:porta]
        var p = raw.Trim().ToLowerInvariant()
                   .Replace("https://", "")
                   .Replace("http://", "")
                   .TrimEnd('/');

        string patternHost = p;
        int? patternPort = null;

        var col = p.IndexOf(':');
        if (col >= 0)
        {
            patternHost = p[..col];
            if (int.TryParse(p[(col + 1)..], out var parsed))
                patternPort = parsed;
        }

        if (patternHost.StartsWith("*.")) // wildcard: *.vercel.app
        {
            var suffix = patternHost[2..]; // "vercel.app"
            if (host == suffix || host.EndsWith("." + suffix))
            {
                if (patternPort is null || patternPort == port) return true;
            }
        }
        else
        {
            if (host == patternHost)
            {
                if (patternPort is null || patternPort == port) return true;
            }
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


// Adicionar serviços do projeto
builder.Services.AddAppServices(builder.Configuration);

// ProblemDetails
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

        if (ctx.HttpContext.Items.TryGetValue("error_code", out var code))
            ctx.ProblemDetails.Extensions["code"] = code;

        if (ctx.HttpContext.Items.TryGetValue("error_meta", out var meta))
            ctx.ProblemDetails.Extensions["meta"] = meta;
    };
});

// Controllers, Swagger, Endpoints
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
    })
    .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secret = jwtSettings["Secret"];
if (string.IsNullOrEmpty(secret))
    secret = "dev-secret";
var key = Encoding.UTF8.GetBytes(secret);

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

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(o =>
    {
        o.ListenLocalhost(5080);
        o.ListenLocalhost(44362, lo => lo.UseHttps());
    });
}
else
{
    // Produção (Render/contêiner)
    var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
    builder.WebHost.UseKestrel().UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

// Middleware pipeline
app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseRouting();
app.UseCors(corsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Map("/error", (HttpContext http) =>
{
    var feature = http.Features.Get<IExceptionHandlerPathFeature>();
    var ex = feature?.Error;

    var (status, code, title, detail, meta) = ErrorMapper.Map(ex);

    http.Items["error_code"] = code;
    if (meta != null) http.Items["error_meta"] = meta;

    if (status == StatusCodes.Status429TooManyRequests &&
        meta?.GetType().GetProperty("retryAfter")?.GetValue(meta) is int ra)
    {
        http.Response.Headers["Retry-After"] = ra.ToString();
    }

    return Results.Problem(
        title: title,
        detail: detail,
        statusCode: status,
        type: $"https://errors.imagino.ai/{code.ToLowerInvariant()}");
});

app.Run();

public partial class Program { }
