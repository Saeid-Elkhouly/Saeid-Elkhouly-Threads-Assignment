using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase1;

public sealed class IncomingOrderRepository
{
    private readonly JsonFileStore _store;

    public IncomingOrderRepository(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        Log.Info("IncomingOrderRepository -> Opening connection...");
        await Task.Delay(200, ct);
        Log.Info("IncomingOrderRepository -> Executing query for incoming orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAllAsync<Order>(DataFiles.IncomingOrders, ct);
    }
}
