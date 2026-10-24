using System.Diagnostics;
using System.Text.Json;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase1;

public sealed class Phase1Runner
{
    private readonly JsonFileStore _store;

    public Phase1Runner(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        DataSeeder seeder = new(_store);
        IncomingOrderRepository incomingRepository = new(_store);

        // Start from clean seed data for the synchronous comparison.
        await seeder.ResetAsync(ct);
        List<Order> incomingOrders = await incomingRepository.GetAllAsync(ct);
        Log.Info($"Loaded {incomingOrders.Count} incoming orders.");

        SyncOrderValidator syncValidator = new(
            new SyncCustomerRepository(_store),
            new SyncProductRepository(_store),
            new SyncCouponRepository(_store));

        Stopwatch syncWatch = Stopwatch.StartNew();
        foreach (Order source in incomingOrders)
        {
            ct.ThrowIfCancellationRequested();
            Order copy = CloneOrder(source);
            _ = syncValidator.ValidateOrder(copy);
        }
        syncWatch.Stop();

        // Reset again so the async run starts from the same original data.
        await seeder.ResetAsync(ct);
        incomingOrders = await incomingRepository.GetAllAsync(ct);

        OrderRepository orderRepository = new(_store);
        OrderValidator validator = new(
            new CustomerRepository(_store),
            new ProductRepository(_store),
            new CouponRepository(_store),
            orderRepository);

        Stopwatch asyncWatch = Stopwatch.StartNew();
        foreach (Order order in incomingOrders)
        {
            ct.ThrowIfCancellationRequested();
            // ValidateOrderAsync saves both accepted and rejected orders to orders.json.
            await validator.ValidateOrderAsync(order, ct);
        }
        asyncWatch.Stop();

        // Counts are read from orders.json, not inferred from in-memory results.
        List<Order> savedOrders = await orderRepository.GetAllAsync(ct);
        int validatedCount = (await orderRepository.GetByStatusAsync(OrderStatus.Validated, ct)).Count;
        int rejectedCount = (await orderRepository.GetByStatusAsync(OrderStatus.Rejected, ct)).Count;

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {savedOrders.Count}");
        Console.WriteLine($"Validated:    {validatedCount}");
        Console.WriteLine($"Rejected:     {rejectedCount}");

        foreach (Order order in savedOrders
                     .Where(order => order.Status == OrderStatus.Rejected)
                     .OrderBy(order => order.Id))
        {
            Console.WriteLine($"  - Order #{order.Id}:  {order.FailureReason}");
        }

        Console.WriteLine($"Sync Time:  {syncWatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"Async Time: {asyncWatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"orders.json now contains {savedOrders.Count} records.");
    }

    private static Order CloneOrder(Order order)
    {
        string json = JsonSerializer.Serialize(order);
        return JsonSerializer.Deserialize<Order>(json)
            ?? throw new InvalidOperationException($"Could not clone Order #{order.Id}.");
    }
}
