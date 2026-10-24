using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase1;

public sealed class CustomerRepository
{
    private readonly JsonFileStore _store;

    public CustomerRepository(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct)
    {
        Log.Info("CustomerRepository -> Opening connection...");
        await Task.Delay(200, ct);
        Log.Info($"CustomerRepository -> Executing query for Customer #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Customer> customers = await _store.ReadAllAsync<Customer>(DataFiles.Customers, ct);
        Customer? customer = customers.FirstOrDefault(c => c.Id == id);

        Log.Info(customer is null
            ? $"CustomerRepository -> Customer #{id} not found"
            : $"CustomerRepository -> Customer #{id} found ({customer.Name})");

        return customer;
    }
}
