using System.Text.Json;
using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Domain.Exceptions;

namespace Ecommerce.Cart.Api.Middleware;

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

        InsufficientStockAppException stockEx => (StatusCodes.Status409Conflict,
            new { message = stockEx.Message }),

        NotFoundAppException notFoundEx => (StatusCodes.Status404NotFound,
            new { message = notFoundEx.Message }),

        HttpRequestException httpEx => (StatusCodes.Status503ServiceUnavailable,
            new { message = "Uno de los servicios necesarios no está disponible en este momento. Intenta de nuevo en unos segundos." }),

        _ => (StatusCodes.Status500InternalServerError,
            new { message = "Ocurrió un error inesperado. Intenta nuevamente más tarde." })
    };
}
