
using Imagino.Api.DependencyInjection;
using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Services.WebhookImage;
using Imagino.Api.Settings;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ImageGeneratorSettings>(
builder.Configuration.GetSection("ImageGeneratorSettings"));
builder.Services.AddSingleton<ImageJobRepository>();

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var conn = config.GetSection("ImageGeneratorSettings")["MongoConnection"];
    return new MongoClient(conn);
});

builder.Services.AddScoped<WebhookImageService>();

builder.WebHost.UseKestrel()
    .UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:44362");

// Registrar serviços da aplicação
builder.Services.AddAppServices(builder.Configuration);

// Adicionar serviços MVC (Controllers)
builder.Services.AddControllers();

// Adicionar Swagger (para testes de API)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Habilitar Swagger em ambiente de dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseStaticFiles(); // pra servir a pasta wwwroot

app.UseHttpsRedirection();
app.UseAuthorization();

// Mapeia os endpoints dos controllers
app.MapControllers();

app.Run();

