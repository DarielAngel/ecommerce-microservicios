using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Features;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Catalog.IntegrationTests;

public class CatalogEndpointsTests : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client;

    public CatalogEndpointsTests(CatalogApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private void AuthenticateAs(string role) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(role));

    private async Task<CategoryResult> CreateCategoryAsAdmin(string name)
    {
        AuthenticateAs("Admin");
        var response = await _client.PostAsJsonAsync("/api/categories", new { Name = name, ParentCategoryId = (Guid?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CategoryResult>())!;
    }

    [Fact]
    public async Task Health_DeberiaResponderOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateCategory_SinToken_DeberiaDevolver401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/categories", new { Name = "Sin auth", ParentCategoryId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCategory_ConRolCliente_DeberiaDevolver403()
    {
        AuthenticateAs("Cliente");

        var response = await _client.PostAsJsonAsync("/api/categories", new { Name = "Rol incorrecto", ParentCategoryId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCategory_ConRolAdmin_DeberiaCrearla()
    {
        var category = await CreateCategoryAsAdmin($"Categoria-{Guid.NewGuid():N}");

        category.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateProduct_ConVariantes_DeberiaCrearloYPermitirBusquedaParcial()
    {
        var category = await CreateCategoryAsAdmin($"Ropa-{Guid.NewGuid():N}");
        var uniqueName = $"Camiseta Edición Especial {Guid.NewGuid():N}";

        AuthenticateAs("Admin");
        var createResponse = await _client.PostAsJsonAsync("/api/products", new
        {
            Name = uniqueName,
            Description = "Camiseta 100% algodón, edición limitada",
            CategoryId = category.Id,
            Variants = new[]
            {
                new { Sku = $"SKU-{Guid.NewGuid():N}", Price = 25.50m, Attributes = new Dictionary<string, string> { ["Talla"] = "M", ["Color"] = "Negro" } },
                new { Sku = $"SKU-{Guid.NewGuid():N}", Price = 27.00m, Attributes = new Dictionary<string, string> { ["Talla"] = "L", ["Color"] = "Negro" } }
            }
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await createResponse.Content.ReadFromJsonAsync<ProductDetailResult>();
        product!.Variants.Should().HaveCount(2);

        // Búsqueda pública, sin token, por una PARTE del nombre (coincidencia parcial).
        _client.DefaultRequestHeaders.Authorization = null;
        var partialTerm = uniqueName.Split(' ')[1]; // ej. "Edición"
        var searchResponse = await _client.GetAsync($"/api/products?searchTerm={Uri.EscapeDataString(partialTerm)}");

        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await searchResponse.Content.ReadFromJsonAsync<PagedResult<ProductSummary>>();
        page!.Items.Should().Contain(p => p.Id == product.Id);
        page.Items.First(p => p.Id == product.Id).MinPrice.Should().Be(25.50m);
    }

    [Fact]
    public async Task SearchProducts_ConFiltroDePrecio_DeberiaExcluirLosFueraDeRango()
    {
        var category = await CreateCategoryAsAdmin($"Electro-{Guid.NewGuid():N}");
        AuthenticateAs("Admin");

        var caroName = $"Producto Caro {Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/products", new
        {
            Name = caroName,
            Description = "Descripción",
            CategoryId = category.Id,
            Variants = new[] { new { Sku = $"SKU-{Guid.NewGuid():N}", Price = 999m, Attributes = new Dictionary<string, string>() } }
        });

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/products?maxPrice=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductSummary>>();
        page!.Items.Should().NotContain(p => p.Name == caroName);
    }

    [Fact]
    public async Task SearchProducts_ConSortByPrecio_DeberiaOrdenarAscendenteYDescendente()
    {
        var category = await CreateCategoryAsAdmin($"Orden-{Guid.NewGuid():N}");
        AuthenticateAs("Admin");

        var baratoName = $"Producto Barato {Guid.NewGuid():N}";
        var caroName = $"Producto Caro {Guid.NewGuid():N}";

        async Task CrearProducto(string name, decimal precio)
        {
            var response = await _client.PostAsJsonAsync("/api/products", new
            {
                Name = name,
                Description = "Descripción",
                CategoryId = category.Id,
                Variants = new[] { new { Sku = $"SKU-{Guid.NewGuid():N}", Price = precio, Attributes = new Dictionary<string, string>() } }
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await CrearProducto(baratoName, 10m);
        await CrearProducto(caroName, 500m);

        _client.DefaultRequestHeaders.Authorization = null;

        var ascResponse = await _client.GetAsync($"/api/products?categoryId={category.Id}&sortBy=price_asc");
        var ascPage = await ascResponse.Content.ReadFromJsonAsync<PagedResult<ProductSummary>>();
        ascPage!.Items.First().Name.Should().Be(baratoName);
        ascPage.Items.Last().Name.Should().Be(caroName);

        var descResponse = await _client.GetAsync($"/api/products?categoryId={category.Id}&sortBy=price_desc");
        var descPage = await descResponse.Content.ReadFromJsonAsync<PagedResult<ProductSummary>>();
        descPage!.Items.First().Name.Should().Be(caroName);
        descPage.Items.Last().Name.Should().Be(baratoName);
    }

    [Fact]
    public async Task SearchProducts_ConSortByInvalido_DeberiaDevolver400()
    {
        var response = await _client.GetAsync("/api/products?sortBy=precio-al-azar");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_ConCategoriaInexistente_DeberiaDevolver404()
    {
        AuthenticateAs("Admin");

        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            Name = "Producto huérfano",
            Description = "Descripción",
            CategoryId = Guid.NewGuid(),
            Variants = new[] { new { Sku = "SKU-X", Price = 10m, Attributes = new Dictionary<string, string>() } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Fase 4: descubrimiento ----

    private async Task<CategoryResult> CreateCategoryAsAdmin(string name, Guid? parentId)
    {
        AuthenticateAs("Admin");
        var response = await _client.PostAsJsonAsync("/api/categories", new { Name = name, ParentCategoryId = parentId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CategoryResult>())!;
    }

    private async Task<ProductDetailResult> CreateProductAsAdmin(string name, Guid categoryId, decimal price = 10m)
    {
        AuthenticateAs("Admin");
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            Name = name,
            Description = "Producto de prueba de descubrimiento.",
            CategoryId = categoryId,
            Variants = new[] { new { Sku = $"DSC-{Guid.NewGuid():N}"[..24], Price = price, Attributes = new Dictionary<string, string>() } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ProductDetailResult>())!;
    }

    private async Task<List<ProductSummary>> GetListAsync(string url)
    {
        _client.DefaultRequestHeaders.Authorization = null; // los endpoints de descubrimiento son públicos
        var response = await _client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<List<ProductSummary>>())!;
    }

    [Fact]
    public async Task Sugerencias_NoDistinguenTildesNiMayusculas_YSonPublicas()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var category = await CreateCategoryAsAdmin($"Audio-{tag}");
        await CreateProductAsAdmin($"Audífonos Zq{tag} Pro", category.Id);

        var result = await GetListAsync($"/api/products/suggestions?q=AUDIFONOS%20ZQ{tag}");

        result.Should().ContainSingle().Which.Name.Should().Be($"Audífonos Zq{tag} Pro");
    }

    [Fact]
    public async Task Sugerencias_PrimeroLasQueEmpiezanConElTexto()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var category = await CreateCategoryAsAdmin($"Celus-{tag}");
        await CreateProductAsAdmin($"Samsung Kx{tag} Galaxy", category.Id);
        await CreateProductAsAdmin($"Kx{tag} Galaxy Lite", category.Id);

        var result = await GetListAsync($"/api/products/suggestions?q=kx{tag}");

        result.Select(p => p.Name).Should().Equal($"Kx{tag} Galaxy Lite", $"Samsung Kx{tag} Galaxy");
    }

    [Fact]
    public async Task Sugerencias_ConUnaSolaLetra_DevuelveListaVacia()
    {
        (await GetListAsync("/api/products/suggestions?q=a")).Should().BeEmpty();
    }

    [Fact]
    public async Task Sugerencias_ElPorcentajeSeBuscaComoTexto_NoComoComodin()
    {
        // Sin escapar, "%" sería un comodín y devolvería cualquier producto.
        (await GetListAsync("/api/products/suggestions?q=%25%25%25")).Should().BeEmpty();
    }

    [Fact]
    public async Task Sugerencias_NoIncluyenProductosDesactivados()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var category = await CreateCategoryAsAdmin($"Inact-{tag}");
        var product = await CreateProductAsAdmin($"Oculto Wv{tag}", category.Id);

        AuthenticateAs("Admin");
        var deactivate = await _client.PutAsJsonAsync($"/api/products/{product.Id}",
            new { Name = product.Name, Description = product.Description, CategoryId = category.Id, IsActive = false });
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetListAsync($"/api/products/suggestions?q=wv{tag}")).Should().BeEmpty();
    }

    [Fact]
    public async Task Relacionados_PrimeroLaMismaCategoria_LuegoLasHermanas_YSinElPropioProducto()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var parent = await CreateCategoryAsAdmin($"Tecno-{tag}", null);
        var phones = await CreateCategoryAsAdmin($"Celulares-{tag}", parent.Id);
        var audio = await CreateCategoryAsAdmin($"Audio-{tag}", parent.Id);
        var unrelated = await CreateCategoryAsAdmin($"Libros-{tag}", null);

        var source = await CreateProductAsAdmin($"Telefono A {tag}", phones.Id);
        var samePhone = await CreateProductAsAdmin($"Telefono B {tag}", phones.Id);
        var sibling = await CreateProductAsAdmin($"Parlante {tag}", audio.Id);
        await CreateProductAsAdmin($"Novela {tag}", unrelated.Id);

        var result = await GetListAsync($"/api/products/{source.Id}/related?limit=10");

        result.Select(p => p.Id).Should().Equal(samePhone.Id, sibling.Id);
    }

    [Fact]
    public async Task Relacionados_DeUnProductoInexistente_DevuelveListaVacia()
    {
        (await GetListAsync($"/api/products/{Guid.NewGuid()}/related")).Should().BeEmpty();
    }

    [Fact]
    public async Task Busqueda_LaTiendaNoVeProductosDesactivados_ElAdminSiConIncludeInactive()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var category = await CreateCategoryAsAdmin($"Ocultos-{tag}");
        var product = await CreateProductAsAdmin($"Desactivado Yq{tag}", category.Id);

        AuthenticateAs("Admin");
        (await _client.PutAsJsonAsync($"/api/products/{product.Id}",
            new { Name = product.Name, Description = product.Description, CategoryId = category.Id, IsActive = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        async Task<int> CountAsync(string? role, bool includeInactive)
        {
            _client.DefaultRequestHeaders.Authorization = role is null ? null
                : new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(role));
            var response = await _client.GetAsync($"/api/products?searchTerm=yq{tag}&includeInactive={includeInactive}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<PagedResult<ProductSummary>>())!.TotalCount;
        }

        (await CountAsync(null, false)).Should().Be(0, "un visitante no ve productos desactivados");
        (await CountAsync(null, true)).Should().Be(0, "pedir includeInactive sin ser Admin no cambia nada");
        (await CountAsync("Cliente", true)).Should().Be(0);
        (await CountAsync("Admin", true)).Should().Be(1, "el panel de Admin los ve para poder reactivarlos");
    }
}
