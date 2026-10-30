namespace OrderProcessingSystem.Phase4;

public sealed class ShippingQuote
{
    public string Carrier { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public List<string> CancelledCarriers { get; init; } = new();
}
