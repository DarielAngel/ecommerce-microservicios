using Ecommerce.Notifications.Application.Common;
using MediatR;

namespace Ecommerce.Notifications.Application.Features;

public record ListRecentNotificationsQuery(int Count = 50) : IRequest<IReadOnlyList<SentNotificationResult>>;

public record SentNotificationResult(Guid Id, string Type, Guid ReferenceId, string RecipientEmail, DateTime SentAtUtc);

public class ListRecentNotificationsQueryHandler
    : IRequestHandler<ListRecentNotificationsQuery, IReadOnlyList<SentNotificationResult>>
{
    private readonly INotificationLogRepository _log;

    public ListRecentNotificationsQueryHandler(INotificationLogRepository log) => _log = log;

    public async Task<IReadOnlyList<SentNotificationResult>> Handle(ListRecentNotificationsQuery request, CancellationToken ct)
    {
        var count = Math.Clamp(request.Count, 1, 200);
        var items = await _log.ListRecentAsync(count, ct);

        return items
            .Select(n => new SentNotificationResult(n.Id, n.Type.ToString(), n.ReferenceId, n.RecipientEmail, n.SentAtUtc))
            .ToList();
    }
}
