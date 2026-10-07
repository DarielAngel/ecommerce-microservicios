using Xunit;

namespace Ecommerce.Users.IntegrationTests;

/// <summary>
/// Todas las clases de prueba de integración comparten UNA sola instancia de <see cref="UsersApiFactory"/> y
/// corren una detrás de otra. Con un IClassFixture por clase, xUnit creaba dos factories en paralelo
/// y cada una pisaba las variables de entorno globales del proceso (cadena de conexión, RabbitMQ) de la
/// otra; al terminar una, las ponía en null mientras la otra seguía arrancando → fallos al azar en CI.
/// </summary>
[CollectionDefinition(Name)]
public class UsersApiCollection : ICollectionFixture<UsersApiFactory>
{
    public const string Name = "Users API";
}
