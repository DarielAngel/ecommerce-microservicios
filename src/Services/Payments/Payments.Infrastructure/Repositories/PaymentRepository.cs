using Ecommerce.Payments.Application.Common;
using Ecommerce.Payments.Domain.Entities;
using Ecommerce.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Payments.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentsDbContext _context;

    public PaymentRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public Task<Payment?> GetByPayPalOrderIdAsync(string payPalOrderId, CancellationToken ct) =>
        _context.Payments.FirstOrDefaultAsync(p => p.PayPalOrderId == payPalOrderId, ct);

    public async Task AddAsync(Payment payment, CancellationToken ct) =>
        await _context.Payments.AddAsync(payment, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}
