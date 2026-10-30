using System.Diagnostics;
using System.Text.Json;
using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase2;

public sealed class Phase2Runner
{
    private readonly JsonFileStore _store;

    public Phase2Runner(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        DataSeeder seeder = new(_store);
        ProductRepository products = new(_store);
        CustomerRepository customers = new(_store);
        CouponRepository coupons = new(_store);
        OrderRepository orders = new(_store);
        IncomingOrderRepository incomingOrdersRepository = new(_store);

        await seeder.ResetAsync(ct);

        RaceConditionDemo raceDemo = new(_store, products);
        await raceDemo.RunAsync(ct);

        // Restore the original stock after deliberately demonstrating both race outcomes.
        await seeder.ResetAsync(ct);

        List<Order> incomingOrders = await incomingOrdersRepository.GetAllAsync(ct);
        PricingService pricingService = new(
            _store,
            customers,
            products,
            coupons,
            new[] { 7 });
        OrderPricer pricer = new(pricingService);

        Order firstOrder = incomingOrders.First(order => order.Id == 1);
        Order sequentialPricingOrder = CloneOrder(firstOrder);
        Order concurrentPricingOrder = CloneOrder(firstOrder);

        Stopwatch sequentialWatch = Stopwatch.StartNew();
        await pricer.PriceOrderSequentialAsync(sequentialPricingOrder, ct);
        sequentialWatch.Stop();

        Stopwatch concurrentWatch = Stopwatch.StartNew();
        await pricer.PriceOrderAsync(concurrentPricingOrder, ct);
        concurrentWatch.Stop();

        Console.WriteLine();
        Console.WriteLine("===== Pricing comparison: Order #1 =====");
        PrintPrice("Sequential", sequentialPricingOrder, sequentialWatch.Elapsed);
        PrintPrice("Concurrent", concurrentPricingOrder, concurrentWatch.Elapsed);

        // Validate all orders concurrently. OrderValidator saves each final validation result.
        OrderValidator validator = new(customers, products, coupons, orders);
        List<Task<Order>> validationTasks = incomingOrders
            .Select(order => validator.ValidateOrderAsync(order, ct))
            .ToList();
        await Task.WhenAll(validationTasks);

        List<Order> validatedOrders = await orders.GetByStatusAsync(OrderStatus.Validated, ct);
        InvoiceGenerator invoiceGenerator = new(_store);
        OrderProcessorV2 processor = new(products, pricer, invoiceGenerator, orders);

        List<Order> processingOrders = new();
        List<Task<bool>> processingTasks = new();
        Stopwatch processingWatch = Stopwatch.StartNew();

        foreach (Order order in validatedOrders)
        {
            ct.ThrowIfCancellationRequested();
            processingOrders.Add(order);
            processingTasks.Add(processor.ProcessValidatedOrderAsync(order, ct));
        }

        if (processingTasks.Count > 0)
        {
            Console.WriteLine($"Task[0] status immediately after creation: {processingTasks[0].Status}");
        }

        await Task.Delay(1000, ct);
        Console.WriteLine("Task statuses after 1 second:");
        foreach (Task<bool> task in processingTasks)
        {
            int index = processingTasks.IndexOf(task);
            Console.WriteLine($"  Order #{processingOrders[index].Id}: {task.Status}");
        }

        foreach (Task<bool> task in processingTasks)
        {
            Order order = processingOrders[processingTasks.IndexOf(task)];
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"Order #{order.Id} task status: {task.Status}. Error: {ex.Message}");

                // In Phase 2, the configured failure occurs after all items have been reserved.
                foreach (OrderItem item in order.Items)
                {
                    await products.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
                }

                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;
                await orders.SaveAsync(order, ct);
            }
        }

        processingWatch.Stop();

        Console.WriteLine();
        Console.WriteLine("===== Final task statuses =====");
        foreach (Task<bool> task in processingTasks)
        {
            Order order = processingOrders[processingTasks.IndexOf(task)];
            Console.WriteLine($"Order #{order.Id}: {task.Status}");
        }
        Console.WriteLine($"Concurrent processing time: {processingWatch.Elapsed.TotalSeconds:F2} seconds");

        List<Order> savedOrders = await orders.GetAllAsync(ct);
        Console.WriteLine($"orders.json contains {savedOrders.Count} records.");

        Console.WriteLine();
        Console.WriteLine("===== Final order summary =====");
        foreach (Order order in savedOrders.OrderBy(order => order.Id))
        {
            Console.WriteLine($"Order #{order.Id,-2} | {order.Status,-12} | Total: {order.TotalAmount,8:F2} | {order.FailureReason}");
        }

        ReportGenerator reportGenerator = new(_store, orders, products);
        Task<Report> reportTask = reportGenerator.GenerateAsync(ct);
        Console.WriteLine("Generating report...");
        Report report = await reportTask;

        Console.WriteLine();
        Console.WriteLine("===== Phase 2 Report =====");
        Console.WriteLine($"Total orders:        {report.TotalOrders}");
        Console.WriteLine($"Total revenue:       {report.TotalRevenue:F2}");
        Console.WriteLine($"Completed:           {report.CompletedCount}");
        Console.WriteLine($"Rejected:            {report.RejectedCount}");
        Console.WriteLine($"Failed:              {report.FailedCount}");
        Console.WriteLine($"Payment failed:      {report.PaymentFailedCount}");
        Console.WriteLine($"Top selling product: {report.TopSellingProduct ?? "None"}");
        Console.WriteLine($"Top selling quantity:{report.TopSellingQuantity,3}");
        Console.WriteLine($"Average order value: {report.AverageOrderValue:F2}");
        Console.WriteLine($"Generated at:        {report.GeneratedAt:O}");
    }

    private static void PrintPrice(string label, Order order, TimeSpan elapsed)
    {
        Console.WriteLine(
            $"{label,-10} | SubTotal={order.SubTotal:F2} | Discount={order.Discount:F2} | Shipping={order.ShippingCost:F2} | Total={order.TotalAmount:F2} | Time={elapsed.TotalSeconds:F2}s");
    }

    private static Order CloneOrder(Order order)
    {
        string json = JsonSerializer.Serialize(order);
        return JsonSerializer.Deserialize<Order>(json)
            ?? throw new InvalidOperationException($"Could not clone Order #{order.Id}.");
    }
}
