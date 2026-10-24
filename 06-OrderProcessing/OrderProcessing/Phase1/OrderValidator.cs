using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase1;

public sealed class OrderValidator
{
    private readonly CustomerRepository _customers;
    private readonly ProductRepository _products;
    private readonly CouponRepository _coupons;
    private readonly OrderRepository _orders;

    public OrderValidator(
        CustomerRepository customers,
        ProductRepository products,
        CouponRepository coupons,
        OrderRepository orders)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _coupons = coupons ?? throw new ArgumentNullException(nameof(coupons));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
    }

    public async Task<Order> ValidateOrderAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        ct.ThrowIfCancellationRequested();

        order.Status = OrderStatus.Pending;
        order.FailureReason = null;

        Customer? customer = await _customers.GetByIdAsync(order.CustomerId, ct);
        if (customer is null)
        {
            return await RejectAndSaveAsync(order, $"Customer #{order.CustomerId} not found", ct);
        }

        if (!customer.IsActive)
        {
            return await RejectAndSaveAsync(order, "Customer is inactive", ct);
        }

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();
            Product? product = await _products.GetByIdAsync(item.ProductId, ct);
            if (product is null)
            {
                return await RejectAndSaveAsync(order, $"Product #{item.ProductId} not found", ct);
            }

            if (product.StockQuantity < item.Quantity)
            {
                return await RejectAndSaveAsync(order, $"Insufficient stock for '{product.Name}'", ct);
            }
        }

        if (order.CouponCode is not null)
        {
            Coupon? coupon = await _coupons.GetByCodeAsync(order.CouponCode, ct);
            if (coupon is null)
            {
                return await RejectAndSaveAsync(order, $"Coupon '{order.CouponCode}' does not exist", ct);
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                return await RejectAndSaveAsync(order, $"Coupon '{order.CouponCode}' has expired", ct);
            }
        }

        order.Status = OrderStatus.Validated;
        order.FailureReason = null;
        await _orders.SaveAsync(order, ct);
        Log.Success($"OrderValidator -> Order #{order.Id} validated.");
        return order;
    }

    private async Task<Order> RejectAndSaveAsync(Order order, string reason, CancellationToken ct)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;
        await _orders.SaveAsync(order, ct);
        Log.Error($"OrderValidator -> Order #{order.Id} rejected: {reason}");
        return order;
    }
}
