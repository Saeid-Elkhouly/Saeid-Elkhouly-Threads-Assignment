using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase2;

public sealed class OrderProcessorV2
{
    private readonly ProductRepository _products;
    private readonly OrderPricer _pricer;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly OrderRepository _orders;

    public OrderProcessorV2(
        ProductRepository products,
        OrderPricer pricer,
        InvoiceGenerator invoiceGenerator,
        OrderRepository orders)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _pricer = pricer ?? throw new ArgumentNullException(nameof(pricer));
        _invoiceGenerator = invoiceGenerator ?? throw new ArgumentNullException(nameof(invoiceGenerator));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
    }

    public async Task<bool> ProcessValidatedOrderAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        List<OrderItem> reservedItems = new();

        Log.Info($"OrderProcessorV2 -> Order #{order.Id}: reserving stock...");

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();
            Product? product = await _products.GetByIdAsync(item.ProductId, ct);
            string productName = product?.Name ?? $"Product #{item.ProductId}";

            bool reserved = await _products.ReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (!reserved)
            {
                foreach (OrderItem reservedItem in reservedItems)
                {
                    await _products.ReleaseStockAsync(reservedItem.ProductId, reservedItem.Quantity, ct);
                }

                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{productName}'";
                await _orders.SaveAsync(order, ct);
                Log.Error($"OrderProcessorV2 -> Order #{order.Id} rejected: {order.FailureReason}");
                return false;
            }

            reservedItems.Add(item);
        }

        await _pricer.PriceOrderAsync(order, ct);
        await _invoiceGenerator.GenerateAsync(order, ct);

        order.Status = OrderStatus.Completed;
        order.FailureReason = null;
        await _orders.SaveAsync(order, ct);

        Log.Success($"OrderProcessorV2 -> Order #{order.Id} completed.");
        return true;
    }
}
