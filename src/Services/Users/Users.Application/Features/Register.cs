using Ecommerce.Contracts.Events;
using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Domain.Entities;
using Ecommerce.Users.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Users.Application.Features;

public record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResult>;

/// <summary>Respuesta compartida por Register, Login y RefreshAccessToken.</summary>
public record AuthResult(Guid UserId, string Email, string FullName, string Role, string AccessToken, string RefreshToken);

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.");
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(2);
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IEventPublisher eventPublisher,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<AuthResult> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, ct))
        {
            throw new ConflictAppException($"Ya existe una cuenta registrada con el email '{request.Email}'.");
        }

        var email = Email.Create(request.Email);
        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.CreateCustomer(email, passwordHash, request.FullName);

        await _userRepository.AddAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        var accessToken = _tokenGenerator.GenerateAccessToken(user);
        var rawRefreshToken = _tokenGenerator.GenerateRefreshToken();
        var refreshTokenEntity = RefreshToken.Create(
            user.Id, _tokenGenerator.Hash(rawRefreshToken), TimeSpan.FromDays(7));

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, ct);
        await _refreshTokenRepository.SaveChangesAsync(ct);

        // Publicamos DESPUÉS de guardar: si RabbitMQ falla, el usuario igual queda registrado
        // (el email de bienvenida es "mejor esfuerzo", no debe romper el registro).
        try
        {
            await _eventPublisher.PublishAsync(
                new UserRegisteredEvent(user.Id, user.Email.Value, user.FullName, DateTime.UtcNow), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo publicar UserRegisteredEvent para el usuario {UserId} (no crítico).", user.Id);
        }

        return new AuthResult(user.Id, user.Email.Value, user.FullName, user.Role.ToString(), accessToken, rawRefreshToken);
    }
}
