using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase4;

public sealed class CustomerDashboard
{
    public Customer Customer { get; init; } = new();
    public List<Order> Orders { get; init; } = new();
    public List<PaymentTransaction> Payments { get; init; } = new();
    public List<EmailMessage> Emails { get; init; } = new();
}
