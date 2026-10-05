using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Domain.Entities;
using Ecommerce.Reviews.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecommerce.Reviews.Infrastructure.Repositories;

public class VerifiedPurchaseRepository : IVerifiedPurchaseRepository
{
    private readonly ReviewsDbContext _context;

    public VerifiedPurchaseRepository(ReviewsDbContext context) => _context = context;

    public Task<bool> HasPurchasedAsync(Guid userId, Guid productId, CancellationToken ct) =>
        _context.VerifiedPurchases.AnyAsync(p => p.UserId == userId && p.ProductId == productId, ct);

    public async Task RecordAsync(Guid userId, IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.ToList();

        var alreadyRecorded = await _context.VerifiedPurchases
            .Where(p => p.UserId == userId && ids.Contains(p.ProductId))
            .Select(p => p.ProductId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var productId in ids.Except(alreadyRecorded))
        {
            _context.VerifiedPurchases.Add(new VerifiedPurchase(userId, productId, now));
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // RabbitMQ entrega "al menos una vez": si otro consumo del mismo evento ganó la carrera,
            // la compra ya está registrada. Que sea idempotente es justamente el objetivo.
        }
    }
}
