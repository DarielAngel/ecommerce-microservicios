using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Application.Features;
using Ecommerce.Users.Domain.Entities;
using Ecommerce.Users.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ecommerce.Users.UnitTests;

public class LoginCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokenGenerator = Substitute.For<IJwtTokenGenerator>();

    private LoginCommandHandler CreateHandler() =>
        new(_userRepository, _refreshTokenRepository, _passwordHasher, _tokenGenerator);

    private static User BuildActiveUser(string email = "cliente@ejemplo.com") =>
        User.CreateCustomer(Email.Create(email), "hash-guardado", "Cliente de Prueba");

    [Fact]
    public async Task Handle_ConCredencialesCorrectas_DeberiaDevolverTokens()
    {
        var user = BuildActiveUser();
        _userRepository.GetByEmailAsync(user.Email.Value, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Password123", "hash-guardado").Returns(true);
        _tokenGenerator.GenerateAccessToken(user).Returns("access-token");
        _tokenGenerator.GenerateRefreshToken().Returns("refresh-token");
        _tokenGenerator.Hash("refresh-token").Returns("refresh-token-hash");

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginCommand(user.Email.Value, "Password123"), CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Handle_ConEmailInexistente_DeberiaLanzarUnauthorizedConMensajeGenerico()
    {
        _userRepository.GetByEmailAsync("noexiste@ejemplo.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(
            new LoginCommand("noexiste@ejemplo.com", "cualquiera"), CancellationToken.None);

        // Regla de seguridad: el mensaje debe ser genérico, no debe decir "usuario no existe".
        (await act.Should().ThrowAsync<UnauthorizedAppException>())
            .WithMessage("Email o contraseña incorrectos.");
    }

    [Fact]
    public async Task Handle_ConPasswordIncorrecta_DeberiaLanzarUnauthorizedConMensajeGenerico()
    {
        var user = BuildActiveUser();
        _userRepository.GetByEmailAsync(user.Email.Value, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("password-incorrecta", "hash-guardado").Returns(false);

        var handler = CreateHandler();
        var act = async () => await handler.Handle(
            new LoginCommand(user.Email.Value, "password-incorrecta"), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedAppException>())
            .WithMessage("Email o contraseña incorrectos.");
    }
}
