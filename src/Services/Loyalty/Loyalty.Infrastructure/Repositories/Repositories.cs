using Ecommerce.Loyalty.Application.Common;
using Ecommerce.Loyalty.Domain.Entities;
using Ecommerce.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecommerce.Loyalty.Infrastructure.Repositories;

public class LoyaltyRepository : ILoyaltyRepository
{
    private readonly LoyaltyDbContext _context;
    private readonly TimeProvider _time;

    public LoyaltyRepository(LoyaltyDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task LockAccountAsync(Guid userId, CancellationToken ct)
    {
        // Crea la cuenta si no existía (sin chocar si otro checkout la crea a la vez) y la bloquea.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO loyalty_accounts (user_id, created_at_utc) VALUES ({userId}, {_time.GetUtcNow().UtcDateTime}) ON CONFLICT (user_id) DO NOTHING",
            ct);
        await _context.Accounts
            .FromSqlInterpolated($"SELECT * FROM loyalty_accounts WHERE user_id = {userId} FOR UPDATE")
            .AsNoTracking()
            .FirstAsync(ct);
    }

    public async Task<IReadOnlyList<LoyaltyEntry>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        await _context.Entries.Where(e => e.UserId == userId).ToListAsync(ct);

    public Task<LoyaltyEntry?> GetAsync(Guid orderId, LoyaltyEntryKind kind, CancellationToken ct) =>
        _context.Entries.FirstOrDefaultAsync(e => e.OrderId == orderId && e.Kind == kind, ct);

    public async Task AddAsync(LoyaltyEntry entry, CancellationToken ct) => await _context.Entries.AddAsync(entry, ct);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly LoyaltyDbContext _context;

    public UnitOfWork(LoyaltyDbContext context) => _context = context;

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        if (_context.Database.CurrentTransaction is not null) return await work();

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Se descarta lo que no se pudo guardar para que el contexto quede usable.
            foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            throw new ConflictAppException("Esa orden ya tiene un movimiento de puntos de ese tipo.");
        }
    }
}
