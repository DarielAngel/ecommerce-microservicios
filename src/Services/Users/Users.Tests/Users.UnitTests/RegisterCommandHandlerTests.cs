using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Application.Features;
using Ecommerce.Users.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Ecommerce.Users.UnitTests;

public class RegisterCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();

    private RegisterCommandHandler CreateHandler() =>
        new(_userRepository, _refreshTokenRepository, _passwordHasher, _tokenGenerator, _eventPublisher, NullLogger<RegisterCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ConEmailNuevo_DeberiaCrearUsuarioYDevolverTokens()
    {
        _userRepository.ExistsByEmailAsync("nuevo@ejemplo.com", Arg.Any<CancellationToken>())
            .Returns(false);
        _passwordHasher.Hash("Password123").Returns("hash-simulado");
        _tokenGenerator.GenerateAccessToken(Arg.Any<User>()).Returns("access-token-simulado");
        _tokenGenerator.GenerateRefreshToken().Returns("refresh-token-simulado");
        _tokenGenerator.Hash("refresh-token-simulado").Returns("refresh-token-hasheado");

        var handler = CreateHandler();
        var command = new RegisterCommand("nuevo@ejemplo.com", "Password123", "Juan Pérez");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Email.Should().Be("nuevo@ejemplo.com");
        result.FullName.Should().Be("Juan Pérez");
        result.Role.Should().Be("Cliente");
        result.AccessToken.Should().Be("access-token-simulado");
        result.RefreshToken.Should().Be("refresh-token-simulado");

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<Ecommerce.Contracts.Events.UserRegisteredEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConEmailYaRegistrado_DeberiaLanzarConflictAppException()
    {
        _userRepository.ExistsByEmailAsync("existente@ejemplo.com", Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new RegisterCommand("existente@ejemplo.com", "Password123", "Juan Pérez");

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictAppException>();
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SiPublicarElEventoFalla_ElRegistroDeberiaCompletarseIgual()
    {
        _userRepository.ExistsByEmailAsync("nuevo2@ejemplo.com", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash("Password123").Returns("hash-simulado");
        _tokenGenerator.GenerateAccessToken(Arg.Any<User>()).Returns("access-token-simulado");
        _tokenGenerator.GenerateRefreshToken().Returns("refresh-token-simulado");
        _tokenGenerator.Hash("refresh-token-simulado").Returns("refresh-token-hasheado");
        _eventPublisher.PublishAsync(Arg.Any<Ecommerce.Contracts.Events.UserRegisteredEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("RabbitMQ caído"));

        var handler = CreateHandler();
        var result = await handler.Handle(
            new RegisterCommand("nuevo2@ejemplo.com", "Password123", "Ana Gómez"), CancellationToken.None);

        // El email de bienvenida es "mejor esfuerzo": si el broker falla, el usuario igual se registra.
        result.Email.Should().Be("nuevo2@ejemplo.com");
    }
}
