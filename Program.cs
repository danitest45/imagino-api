using Imagino.Api.DependencyInjection;
using Imagino.Api.Repository;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Errors;
using Imagino.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Imagino.Api.Security;
using Imagino.Api.Services.Generation;

var builder = WebApplication.CreateBuilder(args);
// Framework URL/exception logging can expose OAuth query strings and upstream bodies.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

// Carregar user-secrets em desenvolvimento
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

var aiStaging = builder.Environment.IsEnvironment("AIStaging");
if (aiStaging)
{
    // Dedicated host: load only its allowlisted profile, never the main app settings.
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddJsonFile("appsettings.AIStaging.json", optional: false)
        .AddEnvironmentVariables().AddCommandLine(args);
    AIStagingConfiguration.Validate(builder.Configuration);
}
else StartupConfiguration.Validate(builder.Configuration, builder.Environment.IsDevelopment());

// Configurações
builder.Services.Configure<ImageGeneratorSettings>(builder.Configuration.GetSection("ImageGeneratorSettings"));
if (!aiStaging) builder.Services.Configure<ReplicateSettings>(builder.Configuration.GetSection("ReplicateSettings"));
builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("Frontend"));
builder.Services.Configure<RefreshTokenCookieSettings>(builder.Configuration.GetSection("RefreshTokenCookie"));
if (!aiStaging) builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

if (!aiStaging)
{
    builder.Services.AddSingleton<ImageJobRepository>();
    builder.Services.AddScoped<WebhookImageService>();
}

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

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy
            .SetIsOriginAllowed(origin => CorsOrigins.Matches(origin, allowedPatterns, exact: aiStaging))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


// Adicionar serviços do projeto
if (aiStaging) builder.Services.AddAIStagingServices(builder.Configuration);
else builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddGenerationV2(builder.Configuration, fixtureOnly: aiStaging);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IRequestLimiter, DurableRequestLimiter>();

// Controllers, Swagger, Endpoints
builder.Services.AddControllers(options =>
{
    if (aiStaging) options.Conventions.Add(new AIStagingControllerConvention());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuthorization.Policy, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  AdminAuthorization.IsConfiguredAdmin(context.User, builder.Configuration)));
});

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

app.UseExceptionHandler("/error");

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
app.UseMiddleware<LaunchRequestMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Map("/error", (HttpContext httpContext) =>
{
    var exception = httpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error
                    ?? new Exception("Unknown error");
    var (status, code, title, detail, meta) = ErrorMapper.Map(exception);
    httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("LaunchOperations")
        .LogWarning("Request failed code={Code} status={Status} type={ErrorType} trace={Trace}", code, status, exception.GetType().Name, httpContext.TraceIdentifier);
    var problem = new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = detail
    };
    problem.Extensions["code"] = code;
    problem.Extensions["traceId"] = httpContext.TraceIdentifier;
    if (meta != null) problem.Extensions["meta"] = meta;
    return TypedResults.Problem(problem);
});

app.Run();

public partial class Program { }
