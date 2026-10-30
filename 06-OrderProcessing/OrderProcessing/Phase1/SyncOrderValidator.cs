using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase1;

public sealed class SyncOrderValidator
{
    private readonly SyncCustomerRepository _customers;
    private readonly SyncProductRepository _products;
    private readonly SyncCouponRepository _coupons;

    public SyncOrderValidator(
        SyncCustomerRepository customers,
        SyncProductRepository products,
        SyncCouponRepository coupons)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _coupons = coupons ?? throw new ArgumentNullException(nameof(coupons));
    }

    public Order ValidateOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        order.Status = OrderStatus.Pending;
        order.FailureReason = null;

        Customer? customer = _customers.GetById(order.CustomerId);
        if (customer is null)
        {
            return Reject(order, $"Customer #{order.CustomerId} not found");
        }

        if (!customer.IsActive)
        {
            return Reject(order, "Customer is inactive");
        }

        foreach (OrderItem item in order.Items)
        {
            Product? product = _products.GetById(item.ProductId);
            if (product is null)
            {
                return Reject(order, $"Product #{item.ProductId} not found");
            }

            if (product.StockQuantity < item.Quantity)
            {
                return Reject(order, $"Insufficient stock for '{product.Name}'");
            }
        }

        if (order.CouponCode is not null)
        {
            Coupon? coupon = _coupons.GetByCode(order.CouponCode);
            if (coupon is null)
            {
                return Reject(order, $"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                return Reject(order, $"Coupon '{order.CouponCode}' has expired");
            }
        }

        order.Status = OrderStatus.Validated;
        return order;
    }

    private static Order Reject(Order order, string reason)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;
        return order;
    }
}
