using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase4;

public interface IShippingCarrier
{
    string Name { get; }
    Task<decimal> GetQuoteAsync(Order order, CancellationToken ct);
}
