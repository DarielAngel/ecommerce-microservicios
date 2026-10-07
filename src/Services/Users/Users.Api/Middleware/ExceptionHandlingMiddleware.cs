using System.Text.Json;
using Ecommerce.Users.Application.Common;
using Ecommerce.Users.Domain.Exceptions;

namespace Ecommerce.Users.Api.Middleware;

/// <summary>
/// Punto único donde las excepciones de negocio se traducen a respuestas HTTP.
/// Los handlers de Application lanzan excepciones "de intención" (Conflict, Unauthorized, etc.)
/// y este middleware decide el código de estado — así los handlers no conocen HTTP.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, payload) = Map(ex);

            if (statusCode >= 500)
            {
                _logger.LogError(ex, "Error no controlado procesando {Path}", context.Request.Path);
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }

    private static (int StatusCode, object Payload) Map(Exception ex) => ex switch
    {
        ValidationAppException validationEx => (StatusCodes.Status400BadRequest,
            (object)new { message = validationEx.Message, errors = validationEx.Errors }),

        // Antes que DomainException: es una de ellas, pero para el cliente es un 404.
        Ecommerce.Users.Domain.Entities.AddressNotFoundException notFoundAddress => (StatusCodes.Status404NotFound,
            new { message = notFoundAddress.Message }),

        DomainException domainEx => (StatusCodes.Status400BadRequest,
            new { message = domainEx.Message }),

        UnauthorizedAppException unauthorizedEx => (StatusCodes.Status401Unauthorized,
            new { message = unauthorizedEx.Message }),

        ConflictAppException conflictEx => (StatusCodes.Status409Conflict,
            new { message = conflictEx.Message }),

        NotFoundAppException notFoundEx => (StatusCodes.Status404NotFound,
            new { message = notFoundEx.Message }),

        _ => (StatusCodes.Status500InternalServerError,
            new { message = "Ocurrió un error inesperado. Intenta nuevamente más tarde." })
    };
}
