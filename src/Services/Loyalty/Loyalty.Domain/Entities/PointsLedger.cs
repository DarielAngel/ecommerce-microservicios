namespace Ecommerce.Loyalty.Domain.Entities;

/// <summary>Cálculos sobre los movimientos de un cliente.</summary>
public static class PointsLedger
{
    /// <summary>
    /// Puntos disponibles: lo ganado (y lo devuelto por devoluciones) menos lo canjeado (confirmado, o apartado
    /// en un checkout de las últimas 2 horas) y lo descontado por devoluciones. Nunca negativo (Lealtad nunca
    /// descuenta por una devolución más de lo que el cliente tiene en ese momento).
    /// </summary>
    public static int Balance(IEnumerable<LoyaltyEntry> entries, DateTime nowUtc)
    {
        var balance = 0;
        foreach (var e in entries.Where(e => e.CountsAt(nowUtc)))
            balance += e.Adds ? e.Points : -e.Points;
        return Math.Max(balance, 0);
    }
}
