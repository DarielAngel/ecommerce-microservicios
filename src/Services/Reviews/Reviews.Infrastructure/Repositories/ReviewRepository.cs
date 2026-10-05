using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Domain.Entities;
using Ecommerce.Reviews.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecommerce.Reviews.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly ReviewsDbContext _context;

    public ReviewRepository(ReviewsDbContext context) => _context = context;

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _context.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Review?> GetByUserAndProductAsync(Guid userId, Guid productId, CancellationToken ct) =>
        _context.Reviews.FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId, ct);

    public async Task AddAsync(Review review, CancellationToken ct) =>
        await _context.Reviews.AddAsync(review, ct);

    public void Remove(Review review) => _context.Reviews.Remove(review);

    public async Task<ReviewPage> ListByProductAsync(Guid productId, ReviewSort sort, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Reviews.AsNoTracking().Where(r => r.ProductId == productId);
        var total = await query.CountAsync(ct);

        var ordered = sort switch
        {
            ReviewSort.Highest => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAtUtc),
            ReviewSort.Lowest => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAtUtc),
            _ => query.OrderByDescending(r => r.CreatedAtUtc)
        };

        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new ReviewPage(items, total);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetRatingDistributionAsync(Guid productId, CancellationToken ct)
    {
        var rows = await _context.Reviews.AsNoTracking()
            .Where(r => r.ProductId == productId)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.Rating, x => x.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, int>>> GetRatingDistributionsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.ToList();

        var rows = await _context.Reviews.AsNoTracking()
            .Where(r => ids.Contains(r.ProductId))
            .GroupBy(r => new { r.ProductId, r.Rating })
            .Select(g => new { g.Key.ProductId, g.Key.Rating, Count = g.Count() })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, int>)g.ToDictionary(x => x.Rating, x => x.Count));
    }

    public async Task MarkVerifiedAsync(Guid userId, IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.ToList();

        await _context.Reviews
            .Where(r => r.UserId == userId && ids.Contains(r.ProductId) && !r.IsVerifiedPurchase)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.IsVerifiedPurchase, true), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos requests simultáneos del mismo usuario para el mismo producto: gana uno, el otro cae acá.
            throw new ConflictAppException("Ya reseñaste este producto. Edita tu reseña existente.");
        }
    }
}
