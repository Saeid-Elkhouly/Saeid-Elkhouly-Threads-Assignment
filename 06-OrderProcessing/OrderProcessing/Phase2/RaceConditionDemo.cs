using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase2;

public sealed class RaceConditionDemo
{
    private const int KeyboardProductId = 4;
    private readonly JsonFileStore _store;
    private readonly ProductRepository _products;

    public RaceConditionDemo(JsonFileStore store, ProductRepository products)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _products = products ?? throw new ArgumentNullException(nameof(products));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== Unsafe stock reservation =====");
        await SetKeyboardStockAsync(1, ct);

        Task<bool> unsafeOrder10 = ReserveStockUnsafeAsync(KeyboardProductId, 1, ct);
        Task<bool> unsafeOrder11 = ReserveStockUnsafeAsync(KeyboardProductId, 1, ct);
        bool[] unsafeResults = await Task.WhenAll(unsafeOrder10, unsafeOrder11);
        int unsafeStock = await GetKeyboardStockAsync(ct);

        Console.WriteLine($"Unsafe results: Order #10 = {unsafeResults[0]}, Order #11 = {unsafeResults[1]}");
        Console.WriteLine($"Keyboard stock after unsafe reservations: {unsafeStock}");

        Console.WriteLine();
        Console.WriteLine("===== Safe stock reservation =====");
        await SetKeyboardStockAsync(1, ct);

        Task<bool> safeOrder10 = _products.ReserveStockAsync(KeyboardProductId, 1, ct);
        Task<bool> safeOrder11 = _products.ReserveStockAsync(KeyboardProductId, 1, ct);
        bool[] safeResults = await Task.WhenAll(safeOrder10, safeOrder11);
        int safeStock = await GetKeyboardStockAsync(ct);

        Console.WriteLine($"Safe results: Order #10 = {safeResults[0]}, Order #11 = {safeResults[1]}");
        Console.WriteLine($"Keyboard stock after safe reservations: {safeStock}");
    }

    public async Task<bool> ReserveStockUnsafeAsync(int productId, int qty, CancellationToken ct)
    {
        Log.Info($"UnsafeReservation -> Reading stock for Product #{productId}...");
        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
        Product? product = products.FirstOrDefault(p => p.Id == productId);

        if (product is null || product.StockQuantity < qty)
        {
            return false;
        }

        int stockRead = product.StockQuantity;

        Log.Info("UnsafeReservation -> Opening connection...");
        await Task.Delay(200, ct);
        Log.Info("UnsafeReservation -> Beginning transaction...");
        await Task.Delay(100, ct);
        Log.Info($"UnsafeReservation -> Updating stock for Product #{productId}...");
        await Task.Delay(400, ct);

        // Deliberately write the stale value read before the delays to demonstrate the race.
        product.StockQuantity = stockRead - qty;
        await _store.WriteAllAsync(DataFiles.Products, products, ct);

        Log.Success($"UnsafeReservation -> Reserved {qty} unit(s) of Product #{productId}.");
        return true;
    }

    private async Task SetKeyboardStockAsync(int quantity, CancellationToken ct)
    {
        await _store.UpdateAsync<Product>(DataFiles.Products, products =>
        {
            Product? keyboard = products.FirstOrDefault(p => p.Id == KeyboardProductId);
            if (keyboard is null)
            {
                throw new InvalidOperationException("Mechanical Keyboard product #4 is missing.");
            }

            keyboard.StockQuantity = quantity;
            return products;
        }, ct);
    }

    private async Task<int> GetKeyboardStockAsync(CancellationToken ct)
    {
        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
        return products.First(p => p.Id == KeyboardProductId).StockQuantity;
    }
}
