
using Imagino.Api.DependencyInjection;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IImageGenerationService, ImageGenerationService>();

builder.Services.Configure<ImageGeneratorSettings>(
    builder.Configuration.GetSection("ImageGeneratorSettings"));

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

