using Ecommerce.Cart.Application.Features;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Cart.Infrastructure.Jobs;

/// <summary>Configuración del recordatorio de carrito abandonado (sección "AbandonedCart").</summary>
public class AbandonedCartOptions
{
    public const string SectionName = "AbandonedCart";

    /// <summary>Apagado no busca carritos ni se conecta a RabbitMQ (lo usan las pruebas de integración).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cuánto sin cambios para considerarlo abandonado. Para probar en local se puede bajar a minutos.</summary>
    public TimeSpan After { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Carritos más viejos que esto no se recuerdan.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Cada cuánto se revisa.</summary>
    public TimeSpan CheckEvery { get; set; } = TimeSpan.FromMinutes(5);
}

public class AbandonedCartWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AbandonedCartOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<AbandonedCartWorker> _logger;

    public AbandonedCartWorker(
        IServiceScopeFactory scopes, IOptions<AbandonedCartOptions> options, TimeProvider time, ILogger<AbandonedCartWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recordatorio de carrito abandonado: tras {After} sin cambios, revisando cada {Every}.",
            _options.After, _options.CheckEvery);

        using var timer = new PeriodicTimer(_options.CheckEvery);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new DetectAbandonedCartsCommand(_time.GetUtcNow().UtcDateTime, _options.After, _options.MaxAge), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un error (base caída, etc.) no detiene el proceso: se reintenta en la próxima vuelta.
                _logger.LogError(ex, "Falló la revisión de carritos abandonados.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
