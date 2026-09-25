using System.Text;
using Ecommerce.Users.Api.Middleware;
using Ecommerce.Users.Api.Options;
using Ecommerce.Users.Application;
using Ecommerce.Users.Infrastructure;
using Ecommerce.Users.Infrastructure.Persistence;
using Ecommerce.Users.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Falta la sección 'Jwt' en la configuración.");

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
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Endpoint especial de creación de Admins protegido por una API key propia
// (no por JWT/rol Admin, porque el primer Admin todavía no existe).
builder.Services.Configure<AdminProvisioningOptions>(
    builder.Configuration.GetSection(AdminProvisioningOptions.SectionName));

var app = builder.Build();

// Crea el esquema de base de datos si no existe (users, refresh_tokens).
// NOTA: esto es EnsureCreated, apropiado para desarrollo local. Para producción real
// se recomienda cambiar a migraciones versionadas de EF Core (dotnet ef migrations add).
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
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

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "users", timestampUtc = DateTime.UtcNow }))
    .AllowAnonymous();

app.MapControllers();

app.Run();

// Necesario para que WebApplicationFactory<Program> encuentre la clase Program en los tests de integración.
public partial class Program { }
