using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase2;

public sealed class PricingService
{
    private readonly JsonFileStore _store;
    private readonly CustomerRepository _customers;
    private readonly ProductRepository _products;
    private readonly CouponRepository _coupons;
    private readonly HashSet<int> _failShippingForOrderIds;

    public PricingService(
        JsonFileStore store,
        CustomerRepository customers,
        ProductRepository products,
        CouponRepository coupons,
        IEnumerable<int> failShippingForOrderIds)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _coupons = coupons ?? throw new ArgumentNullException(nameof(coupons));
        _failShippingForOrderIds = new HashSet<int>(failShippingForOrderIds ?? Array.Empty<int>());
    }

    public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        Log.Info($"PricingService -> Opening connection to price Order #{order.Id}...");
        await Task.Delay(200, ct);
        Log.Info($"PricingService -> Executing product query for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
        decimal subtotal = 0m;

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();
            Product product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Product #{item.ProductId} not found while pricing Order #{order.Id}.");

            item.UnitPrice = RoundMoney(product.Price);
            subtotal += item.UnitPrice * item.Quantity;
        }

        order.SubTotal = RoundMoney(subtotal);
        Log.Success($"PricingService -> Order #{order.Id} subtotal: {order.SubTotal:F2}");
        return order.SubTotal;
    }

    public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        Customer? customer = await _customers.GetByIdAsync(order.CustomerId, ct);
        if (customer is null)
        {
            throw new CustomerNotFoundException($"Customer #{order.CustomerId} not found");
        }

        int loyaltyPercent = customer.LoyaltyTier switch
        {
            LoyaltyTier.Bronze => 0,
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };

        int couponPercent = 0;
        if (order.CouponCode is not null)
        {
            Coupon? coupon = await _coupons.GetByCodeAsync(order.CouponCode, ct);
            if (coupon is null)
            {
                throw new InvalidCouponException($"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                throw new InvalidCouponException($"Coupon '{order.CouponCode}' has expired");
            }

            couponPercent = coupon.DiscountPercent;
        }

        Log.Info($"PricingService -> Discount API handshake for Order #{order.Id}...");
        await Task.Delay(150, ct);
        Log.Info($"PricingService -> Sending discount request for Order #{order.Id}...");
        await Task.Delay(300, ct);
        Log.Info($"PricingService -> Waiting for discount response for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        int discountPercent = Math.Min(loyaltyPercent + couponPercent, 30);
        decimal discount = RoundMoney(order.SubTotal * discountPercent / 100m);
        Log.Success($"PricingService -> Order #{order.Id} discount: {discount:F2} ({discountPercent}%)");
        return discount;
    }

    public async Task<decimal> CalculateShippingCostAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        Log.Info($"PricingService -> Shipping API handshake for Order #{order.Id}...");
        await Task.Delay(150, ct);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            Log.Error($"PricingService -> Shipping provider unavailable for Order #{order.Id}.");
            throw new ShippingProviderUnavailableException("Shipping provider unavailable");
        }

        Log.Info($"PricingService -> Sending shipping request for Order #{order.Id}...");
        await Task.Delay(300, ct);
        Log.Info($"PricingService -> Waiting for shipping response for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
        int totalQuantity = 0;
        decimal totalWeight = 0m;

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();
            Product product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Product #{item.ProductId} not found while calculating shipping for Order #{order.Id}.");

            totalQuantity += item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
        }

        decimal shippingCost = order.SubTotal >= 1000m
            ? 0m
            : 5m + (1.50m * totalQuantity) + (2m * totalWeight);

        shippingCost = RoundMoney(shippingCost);
        Log.Success($"PricingService -> Order #{order.Id} shipping: {shippingCost:F2}");
        return shippingCost;
    }

    public static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
