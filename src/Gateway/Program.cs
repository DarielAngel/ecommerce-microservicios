var builder = WebApplication.CreateBuilder(args);

// El Gateway reenvía las peticiones del frontend a cada microservicio.
// Las rutas se definen en appsettings.json (sección ReverseProxy) y se
// van agregando a medida que cada microservicio queda listo.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        // 5173: reservado para el futuro frontend de clientes. 5174: 'pnpm run dev' del
        // panel de Admin. 8081: el panel ya compilado, servido por nginx en Docker.
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:8081")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("FrontendDev");

// Endpoint de salud simple para verificar que el Gateway está arriba.
// Este es el primer punto de verificación del Paso 1.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "gateway",
    timestampUtc = DateTime.UtcNow
}));

app.MapReverseProxy();

app.Run();
