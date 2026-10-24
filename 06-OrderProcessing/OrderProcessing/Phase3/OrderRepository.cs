using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class OrderRepository
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failSaveForOrderIds;
    private readonly bool _failuresEnabled;

    public OrderRepository(JsonFileStore store, IEnumerable<int>? failSaveForOrderIds = null,
        bool failuresEnabled = true)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _failSaveForOrderIds = new HashSet<int>(failSaveForOrderIds ?? Array.Empty<int>());
        _failuresEnabled = failuresEnabled;
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        Log.Info($"OrderRepository -> Saving Order #{order.Id}...");
        await Task.Delay(200, ct);
        await Task.Delay(100, ct);
        await Task.Delay(400, ct);
        await Task.Delay(200, ct);

        if (_failuresEnabled && order.Status == OrderStatus.Completed &&
            _failSaveForOrderIds.Contains(order.Id))
        {
            throw new DatabaseWriteException("Connection lost during commit");
        }

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

    public Task SaveAsync(Order order) => SaveAsync(order, CancellationToken.None);
}
