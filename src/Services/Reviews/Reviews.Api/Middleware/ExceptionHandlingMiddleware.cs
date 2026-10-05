using System.Text.Json;
using Ecommerce.Reviews.Application.Common;
using Ecommerce.Reviews.Domain.Exceptions;

namespace Ecommerce.Reviews.Api.Middleware;

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

        DomainException domainEx => (StatusCodes.Status400BadRequest,
            new { message = domainEx.Message }),

        NotFoundAppException notFoundEx => (StatusCodes.Status404NotFound,
            new { message = notFoundEx.Message }),

        ConflictAppException conflictEx => (StatusCodes.Status409Conflict,
            new { message = conflictEx.Message }),

        ForbiddenAppException forbiddenEx => (StatusCodes.Status403Forbidden,
            new { message = forbiddenEx.Message }),

        UnauthorizedAccessException unauthorizedEx => (StatusCodes.Status401Unauthorized,
            new { message = unauthorizedEx.Message }),

        _ => (StatusCodes.Status500InternalServerError,
            new { message = "Ocurrió un error inesperado. Intenta nuevamente más tarde." })
    };
}
