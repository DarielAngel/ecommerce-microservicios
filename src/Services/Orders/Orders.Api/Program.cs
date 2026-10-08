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
    // Fase 6 (puntos de lealtad usados en la compra).
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE orders ADD COLUMN IF NOT EXISTS loyalty_points integer NOT NULL DEFAULT 0;");
    await dbContext.Database.ExecuteSqlRawAsync(
        "ALTER TABLE orders ADD COLUMN IF NOT EXISTS loyalty_discount numeric(12,2) NOT NULL DEFAULT 0;");
    // Fase 7: el plazo de devolución cuenta desde el envío. Pedidos enviados antes de la Fase 5 no guardaron esa
    // fecha: se completa una sola vez con su última modificación (antes de la Fase 7 era el momento del envío).
    await dbContext.Database.ExecuteSqlRawAsync(
        "UPDATE orders SET shipped_at_utc = updated_at_utc WHERE status = 'Shipped' AND shipped_at_utc IS NULL;");
    // Fase 7 (devoluciones y reembolsos).
    await dbContext.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS order_returns (
            id uuid PRIMARY KEY,
            order_id uuid NOT NULL REFERENCES orders("Id") ON DELETE CASCADE,
            status character varying(20) NOT NULL,
            reason character varying(30) NOT NULL,
            comment character varying(500) NULL,
            admin_note character varying(500) NULL,
            refund_amount numeric(12,2) NOT NULL,
            loyalty_points_to_restore integer NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            resolved_at_utc timestamp with time zone NULL,
            refunded_at_utc timestamp with time zone NULL);
        CREATE INDEX IF NOT EXISTS "IX_order_returns_order_id" ON order_returns (order_id);
        CREATE INDEX IF NOT EXISTS "IX_order_returns_status" ON order_returns (status);
        CREATE TABLE IF NOT EXISTS order_return_lines (
            id uuid PRIMARY KEY,
            return_id uuid NOT NULL REFERENCES order_returns(id) ON DELETE CASCADE,
            variant_id uuid NOT NULL,
            product_id uuid NOT NULL,
            product_name character varying(200) NOT NULL,
            unit_price numeric(12,2) NOT NULL,
            quantity integer NOT NULL);
        CREATE INDEX IF NOT EXISTS "IX_order_return_lines_return_id" ON order_return_lines (return_id);
        """);
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
