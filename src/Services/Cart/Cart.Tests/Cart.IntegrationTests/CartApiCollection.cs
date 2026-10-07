using Xunit;

namespace Ecommerce.Cart.IntegrationTests;

/// <summary>
/// Todas las clases de prueba de integración comparten UNA sola instancia de <see cref="CartApiFactory"/> y
/// corren una detrás de otra. Con un IClassFixture por clase, xUnit creaba dos factories en paralelo
/// y cada una pisaba las variables de entorno globales del proceso (cadena de conexión, RabbitMQ) de la
/// otra; al terminar una, las ponía en null mientras la otra seguía arrancando → fallos al azar en CI.
/// </summary>
[CollectionDefinition(Name)]
public class CartApiCollection : ICollectionFixture<CartApiFactory>
{
    public const string Name = "Cart API";
}
