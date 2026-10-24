using System.Diagnostics;
using System.Collections.Concurrent;
using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Services;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class OrderProcessor
{
    private readonly CustomerService _customers;
    private readonly InventoryService _inventory;
    private readonly PricingService _pricing;
    private readonly IPaymentGateway _payments;
    private readonly OrderRepository _orders;
    private readonly IEmailService _email;
    private readonly ProductRepository _products;
    private readonly JsonFileStore _store;
    private readonly ConcurrentDictionary<int, byte> _threadIds = new();
    public int ThreadsObserved => _threadIds.Count;

    public OrderProcessor(CustomerService customers, InventoryService inventory, PricingService pricing,
        IPaymentGateway payments, OrderRepository orders, IEmailService email, ProductRepository products,
        JsonFileStore store)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        order.Status = OrderStatus.Pending;
        order.FailureReason = null;
        Customer customer;
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            customer = await _customers.GetCustomerAsync(order.CustomerId, ct);
            Step(order, 1, "Customer lookup", "OK", watch);
        }
        catch (CustomerNotFoundException ex)
        {
            Step(order, 1, "Customer lookup", "NOT FOUND", watch);
            return await StopOrderAsync(order, OrderStatus.Rejected, ex.Message, ct);
        }

        if (!customer.IsActive)
        {
            Step(order, 1, "Customer lookup", "INACTIVE", watch);
            return await StopOrderAsync(order, OrderStatus.Rejected, "Customer is inactive", ct);
        }

        watch.Restart();
        bool reserved = await _inventory.ReserveStockAsync(order, ct);
        if (!reserved)
        {
            OrderItem? unavailable = order.Items.FirstOrDefault(item => item.Quantity <= 0);
            unavailable ??= order.Items.FirstOrDefault();
            string name = unavailable is null ? "unknown product" :
                (await _products.GetByIdAsync(unavailable.ProductId, ct))?.Name ?? $"Product #{unavailable.ProductId}";
            Step(order, 2, "Reserve stock", "INSUFFICIENT", watch);
            return await StopOrderAsync(order, OrderStatus.Rejected, $"Insufficient stock for '{name}'", ct);
        }
        Step(order, 2, "Reserve stock", "OK", watch);

        watch.Restart();
        try
        {
            await _pricing.CalculateTotalAsync(order, ct);
            Step(order, 3, "Calculate total", "OK", watch);
        }
        catch (InvalidCouponException ex)
        {
            await _inventory.ReleaseStockAsync(order, ct);
            Step(order, 3, "Calculate total", "INVALID COUPON; released", watch);
            return await StopOrderAsync(order, OrderStatus.Rejected, ex.Message, ct);
        }

        watch.Restart();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        using CancellationTokenSource paymentCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        PaymentResult payment;
        try
        {
            payment = await _payments.ProcessPaymentAsync(order.Id, order.TotalAmount, paymentCancellation.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Step(order, 4, "Payment", "CANCELLED; handling unresolved by Section 6.6", watch);
            // Section 6.6 is unavailable. Propagate run cancellation without choosing an order status,
            // saving a cancellation result, releasing stock, or refunding a possible capture.
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await _inventory.ReleaseStockAsync(order, ct);
            Step(order, 4, "Payment", "TIMEOUT; released", watch);
            return await StopOrderAsync(order, OrderStatus.PaymentFailed, "Payment gateway timeout (3s)", ct);
        }

        if (!payment.Success)
        {
            await _inventory.ReleaseStockAsync(order, ct);
            Step(order, 4, "Payment", "DECLINED; released", watch);
            return await StopOrderAsync(order, OrderStatus.PaymentFailed,
                payment.Message ?? "Card declined by issuer", ct);
        }
        order.TransactionId = payment.TransactionId;
        Step(order, 4, "Payment", $"OK ({payment.TransactionId})", watch);

        watch.Restart();
        order.Status = OrderStatus.Completed;
        order.FailureReason = null;
        try
        {
            await _orders.SaveAsync(order, ct);
            Step(order, 5, "Save completed order", "OK", watch);
        }
        catch (DatabaseWriteException)
        {
            string transactionId = order.TransactionId!;
            await _payments.RefundAsync(transactionId, ct);
            await _inventory.ReleaseStockAsync(order, ct);
            order.Status = OrderStatus.Failed;
            order.FailureReason = $"Database save failed after payment; payment {transactionId} refunded";
            await _orders.SaveAsync(order, ct);
            Step(order, 5, "Save completed order", "FAILED; refunded and released", watch);
            return false;
        }

        watch.Restart();
        await SendConfirmationWithRetryAsync(order, customer, ct);
        Step(order, 6, "Send confirmation email", "DONE", watch);
        return order.Status == OrderStatus.Completed;
    }

    public Task<bool> ProcessOrderAsync(Order order) => ProcessOrderAsync(order, CancellationToken.None);

    private async Task<bool> StopOrderAsync(Order order, OrderStatus status, string reason, CancellationToken ct)
    {
        order.Status = status;
        order.FailureReason = reason;
        await _orders.SaveAsync(order, ct);
        return false;
    }

    private async Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct)
    {
        int[] delays = { 500, 1000, 2000 };
        SmtpUnavailableException? lastError = null;
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(delays[attempt - 2], ct);
            }

            try
            {
                await _email.SendConfirmationAsync(order, customer, ct);
                return;
            }
            catch (SmtpUnavailableException ex)
            {
                lastError = ex;
                Log.Error($"Order #{order.Id} email attempt {attempt} failed: {ex.Message}");
            }
        }

        EmailMessage retry = new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmation",
            Body = $"Your order is confirmed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = 4,
            LastError = lastError?.Message
        };
        await _store.UpdateAsync<EmailMessage>(DataFiles.EmailRetry, messages =>
        {
            messages.Add(retry);
            return messages;
        }, ct);
        Log.Error($"Email for Order #{order.Id} queued for retry later");
    }

    private void Step(Order order, int number, string action, string outcome, Stopwatch watch)
    {
        _threadIds.TryAdd(Environment.CurrentManagedThreadId, 0);
        watch.Stop();
        Log.Info($"Order #{order.Id} -> Step {number}/6 {action} {outcome} in {watch.ElapsedMilliseconds} ms");
    }
}
