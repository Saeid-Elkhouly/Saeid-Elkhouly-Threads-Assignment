using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase1;

public sealed class OrderRepository
{
    private readonly JsonFileStore _store;

    public OrderRepository(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);
        Log.Info($"OrderRepository -> Beginning transaction for Order #{order.Id}...");
        await Task.Delay(100, ct);
        Log.Info($"OrderRepository -> Inserting/updating Order #{order.Id}...");
        await Task.Delay(400, ct);
        Log.Info("OrderRepository -> Committing transaction...");
        await Task.Delay(200, ct);

        await _store.UpdateAsync<Order>(DataFiles.Orders, orders =>
        {
            int index = orders.FindIndex(existing => existing.Id == order.Id);
            if (index >= 0)
            {
                orders[index] = order;
            }
            else
            {
                orders.Add(order);
            }

            return orders;
        }, ct);

        Log.Success($"OrderRepository -> Order #{order.Id} saved.");
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);
        Log.Info("OrderRepository -> Executing query for all orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct)
    {
        List<Order> orders = await GetAllAsync(ct);
        return orders.Where(order => order.Status == status).ToList();
    }
}
