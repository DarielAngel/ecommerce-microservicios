using Ecommerce.Loyalty.Application.Common;
using Ecommerce.Loyalty.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Loyalty.Application.Features;

/// <summary>
/// Suma los puntos de una compra pagada. Lo dispara el evento OrderPaid de Órdenes, que además deja firme
/// el canje de puntos de esa orden si lo hubo (ver el consumidor): así no depende solo de la llamada
/// "confirm" de Órdenes, que es de mejor esfuerzo.
/// </summary>
public record EarnPointsCommand(Guid UserId, Guid OrderId, decimal AmountPaid) : IRequest;

public class EarnPointsCommandHandler : IRequestHandler<EarnPointsCommand>
{
    private readonly ILoyaltyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ILogger<EarnPointsCommandHandler> _logger;

    public EarnPointsCommandHandler(
        ILoyaltyRepository repository, IUnitOfWork unitOfWork, TimeProvider time, ILogger<EarnPointsCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _time = time;
        _logger = logger;
    }

    public async Task Handle(EarnPointsCommand request, CancellationToken ct)
    {
        // RabbitMQ entrega "al menos una vez": el mismo evento puede llegar dos veces.
        if (await _repository.GetAsync(request.OrderId, LoyaltyEntryKind.Earned, ct) is not null)
        {
            _logger.LogInformation("Los puntos de la orden {OrderId} ya estaban sumados — se ignora el duplicado.", request.OrderId);
            return;
        }

        var entry = LoyaltyEntry.Earn(request.UserId, request.OrderId, request.AmountPaid, _time.GetUtcNow().UtcDateTime);
        if (entry is null) return;

        await _repository.AddAsync(entry, ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConflictAppException)
        {
            // Dos copias del evento a la vez: el índice único dejó pasar solo una. Está bien.
            _logger.LogInformation("Otra copia del evento ya sumó los puntos de la orden {OrderId}.", request.OrderId);
        }
    }
}
