using Ecommerce.Promotions.Domain.Entities;

namespace Ecommerce.Promotions.Application.Common;

/// <summary>Usos de un cupón que cuentan para su límite: confirmados + reservas todavía vigentes.</summary>
public record CouponUsage(int Confirmed, int ActiveReservations)
{
    public int Total => Confirmed + ActiveReservations;
}

public interface ICouponRepository
{
    Task<Coupon?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// Igual que GetByCodeAsync pero bloquea la fila del cupón hasta el fin de la transacción
    /// (SELECT ... FOR UPDATE). Dos checkouts simultáneos con el mismo cupón se atienden de a uno,
    /// así el límite de usos nunca se supera. Solo tiene sentido dentro de ExecuteInTransactionAsync.
    /// </summary>
    Task<Coupon?> GetByCodeForUpdateAsync(string code, CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<Coupon>> ListAsync(CancellationToken ct);
    Task AddAsync(Coupon coupon, CancellationToken ct);
}

public interface IRedemptionRepository
{
    Task<CouponRedemption?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);

    /// <summary>Usos que cuentan para el límite en este momento (las reservas vencidas no cuentan).</summary>
    Task<CouponUsage> GetUsageAsync(Guid couponId, DateTime nowUtc, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, CouponUsage>> GetUsageAsync(IReadOnlyCollection<Guid> couponIds, DateTime nowUtc, CancellationToken ct);

    /// <summary>¿Este cliente ya tiene un uso confirmado o una reserva vigente de este cupón?</summary>
    Task<bool> HasActiveForUserAsync(Guid couponId, Guid userId, DateTime nowUtc, CancellationToken ct);

    Task AddAsync(CouponRedemption redemption, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);

    /// <summary>Guarda. Traduce la violación de unicidad (código de cupón repetido) a ConflictAppException.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
