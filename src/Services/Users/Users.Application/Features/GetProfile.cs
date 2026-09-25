using Ecommerce.Users.Application.Common;
using MediatR;

namespace Ecommerce.Users.Application.Features;

public record GetProfileQuery(Guid UserId) : IRequest<ProfileResult>;

public record ProfileResult(Guid UserId, string Email, string FullName, string Role, DateTime CreatedAtUtc);

public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, ProfileResult>
{
    private readonly IUserRepository _userRepository;

    public GetProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<ProfileResult> Handle(GetProfileQuery request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundAppException("Usuario no encontrado.");

        return new ProfileResult(user.Id, user.Email.Value, user.FullName, user.Role.ToString(), user.CreatedAtUtc);
    }
}
