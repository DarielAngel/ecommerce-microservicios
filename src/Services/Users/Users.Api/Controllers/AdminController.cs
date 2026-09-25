using Ecommerce.Users.Api.Options;
using Ecommerce.Users.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ecommerce.Users.Api.Controllers;

[ApiController]
[Route("api/admins")]
[AllowAnonymous] // La protección real es la API key, verificada manualmente abajo.
public class AdminController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Admin-Provisioning-Key";

    private readonly ISender _mediator;
    private readonly AdminProvisioningOptions _options;

    public AdminController(ISender mediator, IOptions<AdminProvisioningOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    public record CreateAdminRequest(string Email, string Password, string FullName);

    [HttpPost]
    public async Task<ActionResult<CreateAdminResult>> CreateAdmin(CreateAdminRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            // Falla segura: si no hay API key configurada, el endpoint queda cerrado por completo.
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "La creación de administradores no está configurada en este entorno." });
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            providedKey != _options.ApiKey)
        {
            return Unauthorized(new { message = "API key de aprovisionamiento inválida o ausente." });
        }

        var result = await _mediator.Send(
            new CreateAdminCommand(request.Email, request.Password, request.FullName), ct);

        return Created(string.Empty, result);
    }
}
