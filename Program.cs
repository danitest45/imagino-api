
using Imagino.Api.DependencyInjection;
using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
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

app.UseHttpsRedirection();
app.UseAuthorization();

// Mapeia os endpoints dos controllers
app.MapControllers();

app.Run();

