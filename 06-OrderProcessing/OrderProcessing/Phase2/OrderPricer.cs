using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase2;

public sealed class OrderPricer
{
    private readonly PricingService _pricingService;

    public OrderPricer(PricingService pricingService)
    {
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
    }

    public async Task PriceOrderAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        await _pricingService.CalculateSubTotalAsync(order, ct);

        Task<decimal> discountTask = _pricingService.GetDiscountAsync(order, ct);
        Task<decimal> shippingTask = _pricingService.CalculateShippingCostAsync(order, ct);

        await Task.WhenAll(discountTask, shippingTask);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;
        order.TotalAmount = PricingService.RoundMoney(
            order.SubTotal - order.Discount + order.ShippingCost);

        Log.Success($"OrderPricer -> Order #{order.Id}: SubTotal={order.SubTotal:F2}, Discount={order.Discount:F2}, Shipping={order.ShippingCost:F2}, Total={order.TotalAmount:F2}");
    }

    public async Task PriceOrderSequentialAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        Task<decimal> subtotalTask = _pricingService.CalculateSubTotalAsync(order, ct);
        order.SubTotal = await subtotalTask;

        Task<decimal> discountTask = _pricingService.GetDiscountAsync(order, ct);
        order.Discount = await discountTask;

        Task<decimal> shippingTask = _pricingService.CalculateShippingCostAsync(order, ct);
        order.ShippingCost = await shippingTask;

        order.TotalAmount = PricingService.RoundMoney(
            order.SubTotal - order.Discount + order.ShippingCost);

        Log.Success($"OrderPricer (sequential) -> Order #{order.Id}: SubTotal={order.SubTotal:F2}, Discount={order.Discount:F2}, Shipping={order.ShippingCost:F2}, Total={order.TotalAmount:F2}");
    }
}
