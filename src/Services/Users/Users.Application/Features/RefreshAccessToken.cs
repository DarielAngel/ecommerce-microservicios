using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Ecommerce.Users.Application.Features;

public record RefreshAccessTokenCommand(string RefreshToken) : IRequest<AuthResult>;

public class RefreshAccessTokenCommandValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshAccessTokenCommandHandler : IRequestHandler<RefreshAccessTokenCommand, AuthResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public RefreshAccessTokenCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResult> Handle(RefreshAccessTokenCommand request, CancellationToken ct)
    {
        var tokenHash = _tokenGenerator.Hash(request.RefreshToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, ct);

        if (existingToken is null || !existingToken.IsActive)
        {
            throw new UnauthorizedAppException("El refresh token es inválido o expiró. Inicia sesión nuevamente.");
        }

        var user = await _userRepository.GetByIdAsync(existingToken.UserId, ct)
            ?? throw new UnauthorizedAppException("El usuario asociado a este token ya no existe.");

        // Rotación: el refresh token usado queda revocado y se emite uno nuevo.
        existingToken.Revoke();

        var newAccessToken = _tokenGenerator.GenerateAccessToken(user);
        var newRawRefreshToken = _tokenGenerator.GenerateRefreshToken();
        var newRefreshTokenEntity = RefreshToken.Create(
            user.Id, _tokenGenerator.Hash(newRawRefreshToken), TimeSpan.FromDays(7));

        await _refreshTokenRepository.AddAsync(newRefreshTokenEntity, ct);
        await _refreshTokenRepository.SaveChangesAsync(ct);

        return new AuthResult(user.Id, user.Email.Value, user.FullName, user.Role.ToString(), newAccessToken, newRawRefreshToken);
    }
}
