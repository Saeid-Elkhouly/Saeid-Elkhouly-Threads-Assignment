using System.Text;
using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase2;

public sealed class ReportGenerator
{
    private readonly JsonFileStore _store;
    private readonly OrderRepository _orders;
    private readonly ProductRepository _products;

    public ReportGenerator(JsonFileStore store, OrderRepository orders, ProductRepository products)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _products = products ?? throw new ArgumentNullException(nameof(products));
    }

    public async Task<Report> GenerateAsync(CancellationToken ct)
    {
        List<Order> orders = await _orders.GetAllAsync(ct);
        List<Order> completedOrders = orders
            .Where(order => order.Status == OrderStatus.Completed)
            .ToList();

        decimal totalRevenue = PricingService.RoundMoney(
            completedOrders.Sum(order => order.TotalAmount));

        Dictionary<int, int> quantitiesByProduct = new();
        foreach (Order order in completedOrders)
        {
            ct.ThrowIfCancellationRequested();
            foreach (OrderItem item in order.Items)
            {
                quantitiesByProduct[item.ProductId] =
                    quantitiesByProduct.GetValueOrDefault(item.ProductId) + item.Quantity;
            }
        }

        string? topSellingProduct = null;
        int topSellingQuantity = 0;
        int topProductId = int.MaxValue;

        foreach (KeyValuePair<int, int> entry in quantitiesByProduct.OrderBy(entry => entry.Key))
        {
            ct.ThrowIfCancellationRequested();
            Product? product = await _products.GetByIdAsync(entry.Key, ct);
            if (product is null)
            {
                continue;
            }

            if (entry.Value > topSellingQuantity ||
                (entry.Value == topSellingQuantity && entry.Key < topProductId))
            {
                topProductId = entry.Key;
                topSellingQuantity = entry.Value;
                topSellingProduct = product.Name;
            }
        }

        Report report = new()
        {
            TotalOrders = orders.Count,
            TotalRevenue = totalRevenue,
            CompletedCount = orders.Count(order => order.Status == OrderStatus.Completed),
            RejectedCount = orders.Count(order => order.Status == OrderStatus.Rejected),
            FailedCount = orders.Count(order => order.Status == OrderStatus.Failed),
            PaymentFailedCount = orders.Count(order => order.Status == OrderStatus.PaymentFailed),
            TopSellingProduct = topSellingProduct,
            TopSellingQuantity = topSellingQuantity,
            AverageOrderValue = completedOrders.Count == 0
                ? 0m
                : PricingService.RoundMoney(totalRevenue / completedOrders.Count),
            GeneratedAt = DateTime.Now
        };

        string reportsDirectory = Path.Combine(_store.DataDirectory, DataFiles.ReportsDirectory);
        Directory.CreateDirectory(reportsDirectory);
        string fileName = $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string path = Path.Combine(reportsDirectory, fileName);
        string json = System.Text.Json.JsonSerializer.Serialize(report, JsonFileStore.JsonOptions);
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), ct);

        Log.Success($"ReportGenerator -> Wrote {fileName}.");
        return report;
    }
}
