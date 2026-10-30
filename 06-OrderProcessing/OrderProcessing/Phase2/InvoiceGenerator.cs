using System.Text;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase2;

public sealed class InvoiceGenerator
{
    private readonly JsonFileStore _store;

    public InvoiceGenerator(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task GenerateAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        Log.Info($"InvoiceGenerator -> Rendering invoice for Order #{order.Id} on a ThreadPool thread...");

        long renderChecksum = await Task.Run(() =>
        {
            long checksum = 0;
            for (int i = 0; i < 20_000_000; i++)
            {
                if ((i & 0x3FFF) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                }

                checksum = (checksum + ((long)i * 31L)) % 1_000_000_007L;
            }

            return checksum;
        }, ct);

        ct.ThrowIfCancellationRequested();

        string invoiceDirectory = Path.Combine(_store.DataDirectory, DataFiles.InvoicesDirectory);
        Directory.CreateDirectory(invoiceDirectory);
        string path = Path.Combine(invoiceDirectory, $"invoice_{order.Id}.txt");

        StringBuilder content = new();
        content.AppendLine($"Invoice for Order #{order.Id}");
        content.AppendLine($"Customer ID: {order.CustomerId}");
        content.AppendLine($"Created: {order.CreatedAt:O}");
        content.AppendLine();
        content.AppendLine("Items:");

        foreach (OrderItem item in order.Items)
        {
            content.AppendLine($"Product #{item.ProductId} | Quantity: {item.Quantity} | Unit price: {item.UnitPrice:F2} | Line total: {(item.UnitPrice * item.Quantity):F2}");
        }

        content.AppendLine();
        content.AppendLine($"Subtotal: {order.SubTotal:F2}");
        content.AppendLine($"Discount: {order.Discount:F2}");
        content.AppendLine($"Shipping: {order.ShippingCost:F2}");
        content.AppendLine($"Total: {order.TotalAmount:F2}");
        content.AppendLine($"Render checksum: {renderChecksum}");

        await File.WriteAllTextAsync(path, content.ToString(), new UTF8Encoding(false), ct);
        Log.Success($"InvoiceGenerator -> Wrote {Path.GetFileName(path)}.");
    }
}
