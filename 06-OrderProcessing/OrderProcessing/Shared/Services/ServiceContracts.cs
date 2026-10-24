using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Shared.Services;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct);
    Task RefundAsync(string transactionId, CancellationToken ct);
}

public interface IEmailService
{
    Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct);
}
