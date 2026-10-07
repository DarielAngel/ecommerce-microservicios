using System.Text;
using Ecommerce.Orders.Api.Middleware;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Infrastructure;
using Ecommerce.Orders.Infrastructure.Persistence;
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // EnsureCreated no modifica una base que ya existía. Estas dos columnas llegaron con los cupones
    // (Fase 3): se agregan si faltan, así una base local anterior sigue funcionando sin borrarla.
    // (El pendiente de pasar a migraciones de EF Core sigue abierto: ver el README.)
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE orders ADD COLUMN IF NOT EXISTS coupon_code character varying(30) NULL;");
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE orders ADD COLUMN IF NOT EXISTS discount_amount numeric(12,2) NOT NULL DEFAULT 0;");
    // Fase 5 (seguimiento del pedido): cuándo se envió.
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE orders ADD COLUMN IF NOT EXISTS shipped_at_utc timestamp with time zone NULL;");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "orders", timestampUtc = DateTime.UtcNow }))
    .AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program { }
