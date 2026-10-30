using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Phase2;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class PricingService
{
    private readonly OrderProcessingSystem.Phase2.PricingService _pricing;

    public PricingService(JsonFileStore store, CustomerRepository customers,
        ProductRepository products, CouponRepository coupons)
    {
        _pricing = new OrderProcessingSystem.Phase2.PricingService(
            store, customers, products, coupons, Array.Empty<int>());
    }

    public async Task<decimal> CalculateTotalAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        await _pricing.CalculateSubTotalAsync(order, ct);

        Task<decimal> discountTask = _pricing.GetDiscountAsync(order, ct);
        Task<decimal> shippingTask = _pricing.CalculateShippingCostAsync(order, ct);
        await Task.WhenAll(discountTask, shippingTask);

        order.Discount = discountTask.Result;
        order.ShippingCost = shippingTask.Result;
        order.TotalAmount = OrderProcessingSystem.Phase2.PricingService.RoundMoney(
            order.SubTotal - order.Discount + order.ShippingCost);
        return order.TotalAmount;
    }

    public Task<decimal> CalculateTotalAsync(Order order) => CalculateTotalAsync(order, CancellationToken.None);
}
