using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Domain.Entities;
using Ecommerce.Users.Domain.ValueObjects;
using Ecommerce.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Users.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _context;

    public UserRepository(UsersDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        // Se compara el Value Object completo (no un miembro suyo) para que EF Core
        // pueda traducir correctamente la comparación usando el converter configurado en el DbContext.
        var emailVo = Email.Create(email);
        return _context.Users.FirstOrDefaultAsync(u => u.Email == emailVo, ct);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct)
    {
        var emailVo = Email.Create(email);
        return _context.Users.AnyAsync(u => u.Email == emailVo, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct) =>
        await _context.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly UsersDbContext _context;

    public RefreshTokenRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct) =>
        await _context.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct) =>
        _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}

public class AddressRepository : IAddressRepository
{
    private readonly UsersDbContext _context;

    public AddressRepository(UsersDbContext context)
    {
        _context = context;
    }

    public Task<List<Address>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        _context.Addresses.Where(a => a.UserId == userId).ToListAsync(ct);

    public async Task AddAsync(Address address, CancellationToken ct) =>
        await _context.Addresses.AddAsync(address, ct);

    public void Remove(Address address) => _context.Addresses.Remove(address);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}
