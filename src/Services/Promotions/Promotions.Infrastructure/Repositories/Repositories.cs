using Ecommerce.Promotions.Application.Common;
using Ecommerce.Promotions.Domain.Entities;
using Ecommerce.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecommerce.Promotions.Infrastructure.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly PromotionsDbContext _context;

    public CouponRepository(PromotionsDbContext context) => _context = context;

    public Task<Coupon?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _context.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct) =>
        _context.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);

    public Task<Coupon?> GetByCodeForUpdateAsync(string code, CancellationToken ct) =>
        _context.Coupons
            .FromSqlInterpolated($"SELECT * FROM coupons WHERE code = {code} FOR UPDATE")
            .FirstOrDefaultAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        _context.Coupons.AnyAsync(c => c.Code == code, ct);

    public async Task<IReadOnlyList<Coupon>> ListAsync(CancellationToken ct) =>
        await _context.Coupons.AsNoTracking().OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);

    public async Task AddAsync(Coupon coupon, CancellationToken ct) => await _context.Coupons.AddAsync(coupon, ct);
}

public class RedemptionRepository : IRedemptionRepository
{
    private readonly PromotionsDbContext _context;

    public RedemptionRepository(PromotionsDbContext context) => _context = context;

    public Task<CouponRedemption?> GetByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        _context.Redemptions.FirstOrDefaultAsync(r => r.OrderId == orderId, ct);

    /// <summary>Confirmados, más reservas creadas hace menos de ReservationTtl.</summary>
    private IQueryable<CouponRedemption> Counting(DateTime nowUtc)
    {
        var reservedSince = nowUtc - CouponRedemption.ReservationTtl;
        return _context.Redemptions.AsNoTracking().Where(r =>
            r.Status == RedemptionStatus.Confirmed ||
            (r.Status == RedemptionStatus.Reserved && r.CreatedAtUtc > reservedSince));
    }

    public async Task<CouponUsage> GetUsageAsync(Guid couponId, DateTime nowUtc, CancellationToken ct)
    {
        var all = await GetUsageAsync(new[] { couponId }, nowUtc, ct);
        return all.GetValueOrDefault(couponId, new CouponUsage(0, 0));
    }

    public async Task<IReadOnlyDictionary<Guid, CouponUsage>> GetUsageAsync(
        IReadOnlyCollection<Guid> couponIds, DateTime nowUtc, CancellationToken ct)
    {
        var ids = couponIds.ToList();
        var reservedSince = nowUtc - CouponRedemption.ReservationTtl;
        var rows = await _context.Redemptions.AsNoTracking()
            .Where(r => ids.Contains(r.CouponId) && (
                r.Status == RedemptionStatus.Confirmed ||
                r.Status == RedemptionStatus.Restored ||
                (r.Status == RedemptionStatus.Reserved && r.CreatedAtUtc > reservedSince)))
            .GroupBy(r => new { r.CouponId, r.Status })
            .Select(g => new { g.Key.CouponId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.CouponId).ToDictionary(
            g => g.Key,
            g => new CouponUsage(
                g.Where(x => x.Status == RedemptionStatus.Confirmed).Sum(x => x.Count),
                g.Where(x => x.Status == RedemptionStatus.Reserved).Sum(x => x.Count),
                g.Where(x => x.Status == RedemptionStatus.Restored).Sum(x => x.Count)));
    }

    public Task<bool> HasActiveForUserAsync(Guid couponId, Guid userId, DateTime nowUtc, CancellationToken ct) =>
        Counting(nowUtc).AnyAsync(r => r.CouponId == couponId && r.UserId == userId, ct);

    public async Task AddAsync(CouponRedemption redemption, CancellationToken ct) =>
        await _context.Redemptions.AddAsync(redemption, ct);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly PromotionsDbContext _context;

    public UnitOfWork(PromotionsDbContext context) => _context = context;

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        // Si ya hay una transacción abierta (llamada anidada), se reutiliza.
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // Dos Admin creando el mismo código a la vez, o dos reservas de la misma orden.
            throw new ConflictAppException(pg.ConstraintName == "ux_coupons_code"
                ? "Ya existe un cupón con ese código."
                : "Esta orden ya tiene un cupón reservado.");
        }
    }
}
