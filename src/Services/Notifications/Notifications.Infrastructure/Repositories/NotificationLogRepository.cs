using Ecommerce.Notifications.Application.Common;
using Ecommerce.Notifications.Domain.Entities;
using Ecommerce.Notifications.Domain.Enums;
using Ecommerce.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecommerce.Notifications.Infrastructure.Repositories;

public class NotificationLogRepository : INotificationLogRepository
{
    private readonly NotificationsDbContext _context;

    public NotificationLogRepository(NotificationsDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsAsync(NotificationType type, Guid referenceId, CancellationToken ct) =>
        _context.SentNotifications.AnyAsync(n => n.Type == type && n.ReferenceId == referenceId, ct);

    public async Task<bool> TryAddAsync(SentNotification notification, CancellationToken ct)
    {
        _context.SentNotifications.Add(notification);

        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _context.Entry(notification).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<IReadOnlyList<SentNotification>> ListRecentAsync(int count, CancellationToken ct) =>
        await _context.SentNotifications
            .OrderByDescending(n => n.SentAtUtc)
            .Take(count)
            .ToListAsync(ct);
}
