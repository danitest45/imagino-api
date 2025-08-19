
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

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.Configure<ImageGeneratorSettings>(
builder.Configuration.GetSection("ImageGeneratorSettings"));

builder.Services.Configure<ReplicateSettings>(
builder.Configuration.GetSection("ReplicateSettings"));
builder.Services.AddSingleton<ImageJobRepository>();

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetSection("ImageGeneratorSettings")["MongoConnection"];
    return new MongoClient(conn);
});

var corsPolicyName = "AllowFrontend";
var downloadCorsPolicyName = "AllowDownload";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;
                var uri = new Uri(origin);
                return
                    (uri.Scheme == "http" || uri.Scheme == "https") &&
                    (
                        uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                        uri.Host.EndsWith(".ngrok-free.app", StringComparison.OrdinalIgnoreCase)
                    );
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
    options.AddPolicy(downloadCorsPolicyName, policy =>
    {
        policy
            .AllowAnyOrigin()
            .WithMethods("GET")
            .AllowAnyHeader();
    });
});

builder.Services.AddScoped<WebhookImageService>();

builder.WebHost.UseKestrel()
    .UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:44362");

builder.Services.AddAppServices(builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseStaticFiles();

app.UseRouting();

app.UseCors(corsPolicyName);
app.UseAuthentication();

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();

