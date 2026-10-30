using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class InventoryService
{
    private readonly JsonFileStore _store;

    public InventoryService(JsonFileStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        Log.Info($"InventoryService -> Reserving all items for Order #{order.Id}...");
        await Task.Delay(200, ct);
        await Task.Delay(100, ct);
        await Task.Delay(400, ct);
        await Task.Delay(200, ct);

        bool reserved = false;
        await _store.UpdateAsync<Product>(DataFiles.Products, products =>
        {
            var requested = order.Items.GroupBy(item => item.ProductId)
                .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
                .ToList();
            bool canReserve = order.Items.All(item => item.Quantity > 0) && requested.All(request =>
                products.Any(product => product.Id == request.ProductId && product.StockQuantity >= request.Quantity));
            if (!canReserve)
            {
                return products;
            }

            foreach (var request in requested)
            {
                products.First(product => product.Id == request.ProductId).StockQuantity -= request.Quantity;
            }

            reserved = true;
            return products;
        }, ct);

        Log.Info(reserved
            ? $"InventoryService -> Stock reserved for Order #{order.Id}."
            : $"InventoryService -> Stock unavailable for Order #{order.Id}.");
        return reserved;
    }

    public Task<bool> ReserveStockAsync(Order order) => ReserveStockAsync(order, CancellationToken.None);

    public async Task ReleaseStockAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        Log.Info($"InventoryService -> Releasing stock for Order #{order.Id}...");
        await Task.Delay(200, ct);
        await Task.Delay(100, ct);
        await Task.Delay(400, ct);
        await Task.Delay(200, ct);

        await _store.UpdateAsync<Product>(DataFiles.Products, products =>
        {
            foreach (OrderItem item in order.Items)
            {
                Product? product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId);
                if (product is not null)
                {
                    product.StockQuantity += item.Quantity;
                }
            }

            return products;
        }, ct);
        Log.Success($"InventoryService -> Stock released for Order #{order.Id}.");
    }

    public Task ReleaseStockAsync(Order order) => ReleaseStockAsync(order, CancellationToken.None);
}
