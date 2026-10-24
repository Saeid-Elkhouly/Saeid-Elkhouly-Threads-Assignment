using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase1;

public sealed class ProductRepository
{
    private readonly JsonFileStore _store;

    public ProductRepository(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);
        Log.Info($"ProductRepository -> Executing query for Product #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
        Product? product = products.FirstOrDefault(p => p.Id == id);

        Log.Info(product is null
            ? $"ProductRepository -> Product #{id} not found"
            : $"ProductRepository -> Product #{id} found ({product.Name})");

        return product;
    }

    public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct)
    {
        if (qty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qty), "Quantity must be greater than zero.");
        }

        Log.Info("ProductRepository -> Opening connection for stock reservation...");
        await Task.Delay(200, ct);
        Log.Info($"ProductRepository -> Beginning stock transaction for Product #{productId}...");
        await Task.Delay(100, ct);
        Log.Info($"ProductRepository -> Updating stock for Product #{productId}...");
        await Task.Delay(400, ct);
        Log.Info("ProductRepository -> Committing stock transaction...");
        await Task.Delay(200, ct);

        bool reserved = false;
        await _store.UpdateAsync<Product>(DataFiles.Products, products =>
        {
            Product? product = products.FirstOrDefault(p => p.Id == productId);
            if (product is null || product.StockQuantity < qty)
            {
                return products;
            }

            product.StockQuantity -= qty;
            reserved = true;
            return products;
        }, ct);

        Log.Info(reserved
            ? $"ProductRepository -> Reserved {qty} unit(s) of Product #{productId}."
            : $"ProductRepository -> Could not reserve {qty} unit(s) of Product #{productId}.");

        return reserved;
    }
}
