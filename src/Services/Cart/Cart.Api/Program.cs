using System.Text;
using Ecommerce.Cart.Api.Middleware;
using Ecommerce.Cart.Application;
using Ecommerce.Cart.Infrastructure;
using Ecommerce.Cart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Falta 'Jwt:Secret' en la configuración.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Ecommerce.Users";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Ecommerce.Clients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Crea el esquema de base de datos si no existe (carts, cart_items).
// NOTA: EnsureCreated es apropiado para desarrollo local; en producción real se recomienda
// cambiar a migraciones versionadas de EF Core.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CartDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // EnsureCreated no toca una base que ya existía: las columnas del recordatorio de carrito
    // abandonado (Fase 6) se agregan si faltan, así tu base local sigue sirviendo sin borrarla.
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE carts ADD COLUMN IF NOT EXISTS contact_email character varying(256) NULL;");
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE carts ADD COLUMN IF NOT EXISTS contact_name character varying(200) NULL;");
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE carts ADD COLUMN IF NOT EXISTS abandoned_reminder_for_activity_at_utc timestamp with time zone NULL;");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "cart", timestampUtc = DateTime.UtcNow }))
    .AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program { }
