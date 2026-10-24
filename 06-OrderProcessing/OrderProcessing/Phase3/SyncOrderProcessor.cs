using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class SyncOrderProcessor
{
    private readonly SyncServices _services;

    public SyncOrderProcessor(JsonFileStore store) => _services = new SyncServices(store);

    public void ProcessOrder(Order order) => _services.ProcessOrder(order);
}
