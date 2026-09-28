using Ecommerce.Notifications.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Notifications.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "Admin")]
public class NotificationsController : ControllerBase
{
    private readonly ISender _mediator;

    public NotificationsController(ISender mediator) => _mediator = mediator;

    /// <summary>Últimos emails enviados (para auditoría y para verificar el flujo sin abrir el correo).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SentNotificationResult>>> ListRecent([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListRecentNotificationsQuery(count), ct);
        return Ok(result);
    }
}
