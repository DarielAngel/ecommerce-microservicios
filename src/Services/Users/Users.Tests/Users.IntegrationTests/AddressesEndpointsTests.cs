using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Users.Application.Features;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Users.IntegrationTests;

[Collection(UsersApiCollection.Name)]
public class AddressesEndpointsTests
{
    private readonly UsersApiFactory _factory;

    public AddressesEndpointsTests(UsersApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Un cliente nuevo con su propio HttpClient ya autenticado.</summary>
    private async Task<HttpClient> NewCustomerAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"dir-{Guid.NewGuid():N}@ejemplo.com",
            Password = "Password123",
            FullName = "Cliente Direcciones"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static object Input(string label, bool makeDefault = false, string street = "Av. Siempre Viva 742") => new
    {
        Label = label,
        RecipientName = "Ana Martínez",
        Phone = "+54 351 555-1234",
        Street = street,
        Details = "Piso 3",
        City = "Córdoba",
        Region = "Córdoba",
        PostalCode = "5000",
        Country = "Argentina",
        MakeDefault = makeDefault
    };

    private static async Task<AddressResult> CreatedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AddressResult>())!;
    }

    [Fact]
    public async Task SinSesion_Devuelve401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnClienteNuevo_TieneLaLibretaVacia()
    {
        var client = await NewCustomerAsync();

        var list = await client.GetFromJsonAsync<List<AddressResult>>("/api/addresses");

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Crear_LaPrimeraQuedaPredeterminadaYTraeElTextoParaElPedido()
    {
        var client = await NewCustomerAsync();

        var casa = await CreatedAsync(await client.PostAsJsonAsync("/api/addresses", Input("Casa")));

        casa.IsDefault.Should().BeTrue();
        casa.Formatted.Should().Be("Ana Martínez, Av. Siempre Viva 742, Piso 3, 5000 Córdoba, Argentina · Tel. +54 351 555-1234");
        var list = await client.GetFromJsonAsync<List<AddressResult>>("/api/addresses");
        list.Should().ContainSingle().Which.Id.Should().Be(casa.Id);
    }

    [Fact]
    public async Task CambiarLaPredeterminada_YBorrarla_PromueveOtra()
    {
        var client = await NewCustomerAsync();
        var casa = await CreatedAsync(await client.PostAsJsonAsync("/api/addresses", Input("Casa")));
        var oficina = await CreatedAsync(await client.PostAsJsonAsync("/api/addresses", Input("Oficina")));
        oficina.IsDefault.Should().BeFalse();

        var afterDefault = await (await client.PostAsync($"/api/addresses/{oficina.Id}/default", null))
            .Content.ReadFromJsonAsync<List<AddressResult>>();
        afterDefault!.First().Id.Should().Be(oficina.Id, "la predeterminada va primero");
        afterDefault.Single(a => a.Id == casa.Id).IsDefault.Should().BeFalse();

        var afterDelete = await (await client.DeleteAsync($"/api/addresses/{oficina.Id}"))
            .Content.ReadFromJsonAsync<List<AddressResult>>();
        afterDelete.Should().ContainSingle().Which.Should().Match<AddressResult>(a => a.Id == casa.Id && a.IsDefault);
    }

    [Fact]
    public async Task Editar_ActualizaLosDatos()
    {
        var client = await NewCustomerAsync();
        var casa = await CreatedAsync(await client.PostAsJsonAsync("/api/addresses", Input("Casa")));

        var response = await client.PutAsJsonAsync($"/api/addresses/{casa.Id}", Input("Casa nueva", street: "Bv. San Juan 100"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AddressResult>();
        updated!.Label.Should().Be("Casa nueva");
        updated.Street.Should().Be("Bv. San Juan 100");
        updated.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task DatosIncompletos_Devuelve400ConElCampo()
    {
        var client = await NewCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/addresses", new { Label = "Casa", RecipientName = "Ana", City = "Lima", Country = "Perú" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("errors").GetProperty("Input.Street")[0].GetString().Should().Be("Escribe la calle y el número.");
    }

    [Fact]
    public async Task LaDireccionDeOtroCliente_NoSePuedeVerNiTocar()
    {
        var ana = await NewCustomerAsync();
        var luis = await NewCustomerAsync();
        var deAna = await CreatedAsync(await ana.PostAsJsonAsync("/api/addresses", Input("Casa")));

        (await luis.PutAsJsonAsync($"/api/addresses/{deAna.Id}", Input("Mía"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await luis.DeleteAsync($"/api/addresses/{deAna.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await luis.PostAsync($"/api/addresses/{deAna.Id}/default", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await luis.GetFromJsonAsync<List<AddressResult>>("/api/addresses")).Should().BeEmpty();

        var deAnaIntacta = await ana.GetFromJsonAsync<List<AddressResult>>("/api/addresses");
        deAnaIntacta.Should().ContainSingle().Which.Label.Should().Be("Casa");
    }
}
