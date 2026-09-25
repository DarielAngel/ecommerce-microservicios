using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Domain.Entities;
using Ecommerce.Users.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Ecommerce.Users.Application.Features;

public record CreateAdminCommand(string Email, string Password, string FullName) : IRequest<CreateAdminResult>;

public record CreateAdminResult(Guid UserId, string Email, string FullName);

public class CreateAdminCommandValidator : AbstractValidator<CreateAdminCommand>
{
    public CreateAdminCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(2);
    }
}

public class CreateAdminCommandHandler : IRequestHandler<CreateAdminCommand, CreateAdminResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public CreateAdminCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<CreateAdminResult> Handle(CreateAdminCommand request, CancellationToken ct)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, ct))
        {
            throw new ConflictAppException($"Ya existe una cuenta registrada con el email '{request.Email}'.");
        }

        var email = Email.Create(request.Email);
        var passwordHash = _passwordHasher.Hash(request.Password);
        var admin = User.CreateAdmin(email, passwordHash, request.FullName);

        await _userRepository.AddAsync(admin, ct);
        await _userRepository.SaveChangesAsync(ct);

        return new CreateAdminResult(admin.Id, admin.Email.Value, admin.FullName);
    }
}
