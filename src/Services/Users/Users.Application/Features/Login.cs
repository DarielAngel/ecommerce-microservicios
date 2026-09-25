using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Users.Application.Features;

public record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken ct)
    {
        // Mensaje de error genérico a propósito: no revelamos si falló el email o la contraseña
        // (evita que un atacante pueda enumerar qué emails están registrados).
        const string genericError = "Email o contraseña incorrectos.";

        var user = await _userRepository.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedAppException(genericError);

        if (!user.IsActive)
        {
            throw new UnauthorizedAppException("Esta cuenta está desactivada.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAppException(genericError);
        }

        var accessToken = _tokenGenerator.GenerateAccessToken(user);
        var rawRefreshToken = _tokenGenerator.GenerateRefreshToken();
        var refreshTokenEntity = RefreshToken.Create(
            user.Id, _tokenGenerator.Hash(rawRefreshToken), TimeSpan.FromDays(7));

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, ct);
        await _refreshTokenRepository.SaveChangesAsync(ct);

        return new AuthResult(user.Id, user.Email.Value, user.FullName, user.Role.ToString(), accessToken, rawRefreshToken);
    }
}
