using Ecommerce.Users.Domain.Exceptions;
using Ecommerce.Users.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Ecommerce.Users.UnitTests;

public class EmailTests
{
    [Theory]
    [InlineData("cliente@ejemplo.com")]
    [InlineData("Cliente@Ejemplo.COM")]
    [InlineData("nombre.apellido+tag@dominio.co")]
    public void Create_ConEmailValido_DeberiaCrearseYNormalizarseAMinusculas(string input)
    {
        var email = Email.Create(input);

        email.Value.Should().Be(input.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-email")]
    [InlineData("falta-dominio@")]
    [InlineData("@falta-usuario.com")]
    public void Create_ConEmailInvalido_DeberiaLanzarDomainException(string input)
    {
        var act = () => Email.Create(input);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Equals_ConMismoValor_DeberianSerIguales()
    {
        var email1 = Email.Create("cliente@ejemplo.com");
        var email2 = Email.Create("CLIENTE@ejemplo.com");

        email1.Should().Be(email2);
        (email1 == email2 || email1.Equals(email2)).Should().BeTrue();
    }
}
