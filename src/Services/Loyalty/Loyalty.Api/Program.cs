using System.Text;
using Ecommerce.Loyalty.Api.Middleware;
using Ecommerce.Loyalty.Application;
using Ecommerce.Loyalty.Infrastructure;
using Ecommerce.Loyalty.Infrastructure.Persistence;
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
// Enums como texto ("Earned" / "Redeemed") tanto al leer como al escribir JSON.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Crea el esquema (loyalty_accounts, loyalty_entries) si no existe.
// NOTA: EnsureCreated es apropiado para desarrollo local; ver "Troubleshooting" del README sobre
// esquemas desactualizados y el pendiente de migrar a migraciones versionadas de EF Core.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // Fase 7 (devoluciones): una orden puede tener varios ajustes, uno por devolución. En una base de la Fase 6
    // se agrega la columna y se cambia el índice único (orden, tipo) por (orden, tipo, referencia).
    await dbContext.Database.ExecuteSqlRawAsync("""
        ALTER TABLE loyalty_entries ADD COLUMN IF NOT EXISTS reference_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        DROP INDEX IF EXISTS ux_loyalty_entries_order_kind;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_loyalty_entries_order_kind_ref ON loyalty_entries (order_id, kind, reference_id);
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

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "loyalty", timestampUtc = DateTime.UtcNow }))
    .AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program { }
