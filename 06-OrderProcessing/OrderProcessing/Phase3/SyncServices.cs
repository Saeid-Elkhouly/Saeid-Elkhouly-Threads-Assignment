using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class SyncServices
{
    private static readonly object InventoryLock = new();
    private static readonly object PaymentLock = new();
    private static readonly object EmailLock = new();
    private static readonly object OrdersLock = new();
    private readonly JsonFileStore _store;

    public SyncServices(JsonFileStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public void ProcessOrder(Order order)
    {
        Thread.Sleep(200);
        Thread.Sleep(400);
        Customer? customer = _store.ReadAll<Customer>(DataFiles.Customers)
            .FirstOrDefault(candidate => candidate.Id == order.CustomerId);
        if (customer is null || !customer.IsActive)
        {
            Finish(order, OrderStatus.Rejected,
                customer is null ? $"Customer #{order.CustomerId} not found" : "Customer is inactive");
            return;
        }

        Thread.Sleep(200);
        Thread.Sleep(300);
        bool reserved = Reserve(order);
        if (!reserved)
        {
            OrderItem item = order.Items[0];
            string name = _store.ReadAll<Product>(DataFiles.Products)
                .FirstOrDefault(product => product.Id == item.ProductId)?.Name ?? $"Product #{item.ProductId}";
            Finish(order, OrderStatus.Rejected, $"Insufficient stock for '{name}'");
            return;
        }

        Thread.Sleep(500);
        List<Product> products = _store.ReadAll<Product>(DataFiles.Products);
        decimal subtotal = 0m;
        int quantity = 0;
        decimal weight = 0m;
        foreach (OrderItem item in order.Items)
        {
            Product product = products.First(candidate => candidate.Id == item.ProductId);
            item.UnitPrice = product.Price;
            subtotal += item.UnitPrice * item.Quantity;
            quantity += item.Quantity;
            weight += product.WeightKg * item.Quantity;
        }
        order.SubTotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);

        Thread.Sleep(150);
        Thread.Sleep(300);
        Thread.Sleep(700);
        int loyalty = customer.LoyaltyTier switch
        {
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };
        int couponDiscount = 0;
        if (order.CouponCode is not null)
        {
            Thread.Sleep(200);
            Thread.Sleep(400);
            Coupon? coupon = _store.ReadAll<Coupon>(DataFiles.Coupons)
                .FirstOrDefault(value => string.Equals(value.Code, order.CouponCode, StringComparison.OrdinalIgnoreCase));
            if (coupon is null || coupon.ExpiryDate.Date < DateTime.Today)
            {
                Release(order);
                Finish(order, OrderStatus.Rejected, $"Coupon '{order.CouponCode}' is invalid or expired");
                return;
            }
            couponDiscount = coupon.DiscountPercent;
        }
        order.Discount = Math.Round(order.SubTotal * Math.Min(loyalty + couponDiscount, 30) / 100m,
            2, MidpointRounding.AwayFromZero);
        Thread.Sleep(150);
        Thread.Sleep(300);
        Thread.Sleep(700);
        order.ShippingCost = order.SubTotal >= 1000m ? 0m :
            Math.Round(5m + 1.50m * quantity + 2m * weight, 2, MidpointRounding.AwayFromZero);
        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost,
            2, MidpointRounding.AwayFromZero);

        Thread.Sleep(200);
        Thread.Sleep(700);
        string transactionId = $"TXN-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        lock (PaymentLock)
        {
            List<PaymentTransaction> transactions = _store.ReadAll<PaymentTransaction>(DataFiles.Payments);
            transactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Status = PaymentStatus.Captured,
                TransactionId = transactionId,
                CreatedAt = DateTime.Now
            });
            _store.WriteAll(DataFiles.Payments, transactions);
        }
        order.TransactionId = transactionId;
        order.Status = OrderStatus.Completed;
        order.FailureReason = null;
        SaveOrder(order);

        Thread.Sleep(500);
        lock (EmailLock)
        {
            List<EmailMessage> emails = _store.ReadAll<EmailMessage>(DataFiles.Emails);
            emails.Add(new EmailMessage
            {
                Id = Guid.NewGuid(), OrderId = order.Id, To = customer.Email,
                Subject = $"Order #{order.Id} confirmation", Body = "Order confirmed",
                SentAt = DateTime.Now, Attempts = 1
            });
            _store.WriteAll(DataFiles.Emails, emails);
        }
    }

    private bool Reserve(Order order)
    {
        Thread.Sleep(200);
        Thread.Sleep(100);
        Thread.Sleep(400);
        Thread.Sleep(200);
        lock (InventoryLock)
        {
            List<Product> products = _store.ReadAll<Product>(DataFiles.Products);
            if (!order.Items.All(item => item.Quantity > 0 && products.Any(product =>
                    product.Id == item.ProductId && product.StockQuantity >= item.Quantity)))
            {
                return false;
            }
            foreach (OrderItem item in order.Items)
            {
                products.First(product => product.Id == item.ProductId).StockQuantity -= item.Quantity;
            }
            _store.WriteAll(DataFiles.Products, products);
            return true;
        }
    }

    private void Release(Order order)
    {
        lock (InventoryLock)
        {
            List<Product> products = _store.ReadAll<Product>(DataFiles.Products);
            foreach (OrderItem item in order.Items)
            {
                Product? product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId);
                if (product is not null) product.StockQuantity += item.Quantity;
            }
            _store.WriteAll(DataFiles.Products, products);
        }
    }

    private void Finish(Order order, OrderStatus status, string reason)
    {
        order.Status = status;
        order.FailureReason = reason;
        SaveOrder(order);
    }

    private void SaveOrder(Order order)
    {
        Thread.Sleep(200);
        Thread.Sleep(100);
        Thread.Sleep(400);
        Thread.Sleep(200);
        lock (OrdersLock)
        {
            List<Order> orders = _store.ReadAll<Order>(DataFiles.Orders);
            int index = orders.FindIndex(existing => existing.Id == order.Id);
            if (index < 0) orders.Add(order); else orders[index] = order;
            _store.WriteAll(DataFiles.Orders, orders);
        }
    }
}
