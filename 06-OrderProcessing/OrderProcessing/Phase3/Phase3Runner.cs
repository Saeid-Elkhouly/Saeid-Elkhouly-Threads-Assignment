using System.Diagnostics;
using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class Phase3Runner
{
    private readonly JsonFileStore _store;

    public Phase3Runner(JsonFileStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task RunAsync(CancellationToken ct)
    {
        await RunComparisonAsync(ct);
        await new DataSeeder(_store).ResetAsync(ct);

        IncomingOrderRepository incomingRepository = new(_store);
        List<Order> incomingOrders = await incomingRepository.GetAllAsync(ct);
        OrderProcessor processor = CreateProcessor(failuresEnabled: true);
        List<Task<bool>> tasks = new();
        Stopwatch watch = Stopwatch.StartNew();
        foreach (Order order in incomingOrders)
        {
            ct.ThrowIfCancellationRequested();
            tasks.Add(processor.ProcessOrderAsync(order, ct));
        }

        foreach (Task<bool> task in tasks)
        {
            await task;
        }
        watch.Stop();

        List<Order> saved = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        Console.WriteLine();
        Console.WriteLine("===== Phase 3 checkout results =====");
        Console.WriteLine($"{"Order",-7} | {"Status",-14} | {"Total",10} | Reason");
        foreach (Order order in saved.OrderBy(value => value.Id))
        {
            Console.WriteLine($"#{order.Id,-6} | {order.Status,-14} | {order.TotalAmount,10:F2} | {order.FailureReason}");
        }
        Console.WriteLine($"Processed {tasks.Count} orders in {watch.Elapsed.TotalSeconds:F2}s; {processor.ThreadsObserved} thread(s) observed.");
        await PrintFileSummaryAsync(ct);
    }

    private async Task RunComparisonAsync(CancellationToken ct)
    {
        DataSeeder seeder = new(_store);
        IncomingOrderRepository incomingRepository = new(_store);

        await seeder.ResetAsync(ct);
        List<Order> syncOrders = (await incomingRepository.GetAllAsync(ct)).Where(order => order.Id <= 10).ToList();
        SyncOrderProcessor syncProcessor = new(_store);
        System.Collections.Concurrent.ConcurrentDictionary<int, byte> syncThreads = new();
        List<Thread> threads = syncOrders.Select(order => new Thread(() =>
        {
            syncThreads.TryAdd(Environment.CurrentManagedThreadId, 0);
            syncProcessor.ProcessOrder(order);
        })).ToList();
        int syncPoolBefore = ThreadPool.ThreadCount;
        Stopwatch syncWatch = Stopwatch.StartNew();
        foreach (Thread thread in threads) thread.Start();
        foreach (Thread thread in threads) thread.Join();
        syncWatch.Stop();
        int syncPoolAfter = ThreadPool.ThreadCount;
        List<Order> syncResults = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        PrintComparison("Sync", syncWatch.Elapsed, syncThreads.Count, syncPoolBefore, syncPoolAfter, syncResults);

        await seeder.ResetAsync(ct);
        List<Order> asyncOrders = (await incomingRepository.GetAllAsync(ct)).Where(order => order.Id <= 10).ToList();
        OrderProcessor asyncProcessor = CreateProcessor(failuresEnabled: false);
        List<Task<bool>> asyncTasks = new();
        int asyncPoolBefore = ThreadPool.ThreadCount;
        Stopwatch asyncWatch = Stopwatch.StartNew();
        foreach (Order order in asyncOrders)
        {
            asyncTasks.Add(asyncProcessor.ProcessOrderAsync(order, ct));
        }
        foreach (Task<bool> task in asyncTasks)
        {
            await task;
        }
        asyncWatch.Stop();
        int asyncPoolAfter = ThreadPool.ThreadCount;
        List<Order> asyncResults = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        PrintComparison("Async", asyncWatch.Elapsed, asyncProcessor.ThreadsObserved,
            asyncPoolBefore, asyncPoolAfter, asyncResults);

        Console.WriteLine("Async does not make an individual I/O operation faster; it lets a thread work on another order while I/O is pending.");
        Console.WriteLine("Async scales better because waiting orders do not each occupy a blocked thread.");
        Console.WriteLine("With 1,000 synchronous orders, 1,000 dedicated threads consume substantial memory and scheduler time.");
        Console.WriteLine("Async is essential for database, file, and API waits because it increases throughput without a thread per wait.");
    }

    private OrderProcessor CreateProcessor(bool failuresEnabled)
    {
        CustomerRepository customers = new(_store);
        ProductRepository products = new(_store);
        CouponRepository coupons = new(_store);
        return new OrderProcessor(
            new CustomerService(customers),
            new InventoryService(_store),
            new PricingService(_store, customers, products, coupons),
            new PaymentGateway(_store, new[] { 5 }, new[] { 8 }, failuresEnabled),
            new OrderRepository(_store, new[] { 13 }, failuresEnabled),
            new EmailService(_store, new Dictionary<int, int> { [3] = 2, [17] = int.MaxValue }, failuresEnabled),
            products,
            _store);
    }

    private static void PrintComparison(string label, TimeSpan elapsed, int threadsUsed,
        int poolBefore, int poolAfter, List<Order> results)
    {
        int completed = results.Count(order => order.Status == OrderStatus.Completed);
        int rejected = results.Count(order => order.Status == OrderStatus.Rejected);
        Console.WriteLine();
        Console.WriteLine($"===== {label} comparison =====");
        Console.WriteLine($"Completed: {completed}; Rejected: {rejected}");
        Console.WriteLine($"Elapsed: {elapsed.TotalSeconds:F2}s; threads observed: {threadsUsed}; ThreadPool.ThreadCount: {poolBefore} -> {poolAfter}");
        foreach (Order order in results.Where(order => order.Status == OrderStatus.Rejected).OrderBy(order => order.Id))
        {
            Console.WriteLine($"Order #{order.Id}: {order.FailureReason}");
        }
    }

    private async Task PrintFileSummaryAsync(CancellationToken ct)
    {
        List<PaymentTransaction> payments = await _store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, ct);
        List<EmailMessage> emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        List<EmailMessage> retry = await _store.ReadAllAsync<EmailMessage>(DataFiles.EmailRetry, ct);
        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
        Log.Info($"orders.json: {(await _store.ReadAllAsync<Order>(DataFiles.Orders, ct)).Count} records");
        Log.Info($"payments.json: {payments.Count} records ({payments.Count(item => item.Status == PaymentStatus.Captured)} captured, {payments.Count(item => item.Status == PaymentStatus.Declined)} declined, {payments.Count(item => item.Status == PaymentStatus.Refunded)} refunded)");
        Log.Info($"emails.json: {emails.Count} records; email_retry.json: {retry.Count} record(s)");
        Product? keyboard = products.FirstOrDefault(product => product.Name == "Mechanical Keyboard");
        Log.Info($"Mechanical Keyboard stock: {keyboard?.StockQuantity}");
    }
}
