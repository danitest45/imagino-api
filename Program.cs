
using Imagino.Api.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Registrar servi�os da aplica��o
builder.Services.AddAppServices(builder.Configuration);

// Adicionar servi�os MVC (Controllers)
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

