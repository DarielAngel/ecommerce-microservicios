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
