using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Users.Application.Features;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Users.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<UsersApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(UsersApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"test-{Guid.NewGuid():N}@ejemplo.com";

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ConDatosValidos_DeberiaCrearUsuarioYDevolverTokens()
    {
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password123",
            FullName = "Usuario de Prueba"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(email);
        body.Role.Should().Be("Cliente");
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_ConEmailDuplicado_DeberiaDevolver409()
    {
        var email = UniqueEmail();
        var payload = new { Email = email, Password = "Password123", FullName = "Usuario Duplicado" };

        var primeraRespuesta = await _client.PostAsJsonAsync("/api/auth/register", payload);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var segundaRespuesta = await _client.PostAsJsonAsync("/api/auth/register", payload);

        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_ConPasswordCorta_DeberiaDevolver400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = UniqueEmail(),
            Password = "123",
            FullName = "Usuario Prueba"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ConCredencialesCorrectas_DeberiaDevolverTokens()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password123",
            FullName = "Usuario Login"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ConPasswordIncorrecta_DeberiaDevolver401()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password123",
            FullName = "Usuario Login"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "PasswordIncorrecta"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_SinToken_DeberiaDevolver401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_ConTokenValido_DeberiaDevolverElPerfilDelUsuario()
    {
        var email = UniqueEmail();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password123",
            FullName = "Usuario Perfil"
        });
        var tokens = await registerResponse.Content.ReadFromJsonAsync<AuthResult>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResult>();
        profile!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Refresh_ConTokenValido_DeberiaRotarloYDevolverUnoNuevo()
    {
        var email = UniqueEmail();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password123",
            FullName = "Usuario Refresh"
        });
        var tokens = await registerResponse.Content.ReadFromJsonAsync<AuthResult>();

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = tokens!.RefreshToken
        });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<AuthResult>();
        newTokens!.RefreshToken.Should().NotBe(tokens.RefreshToken);

        // El token viejo ya fue rotado (revocado): reutilizarlo debe fallar.
        var reuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = tokens.RefreshToken
        });
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
