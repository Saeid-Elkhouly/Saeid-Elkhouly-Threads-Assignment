using System.Diagnostics;
using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Phase3;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase4;

public sealed class Phase4Runner
{
    private readonly JsonFileStore _store;

    public Phase4Runner(JsonFileStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"Phase 4 data directory: {_store.DataDirectory}");
        await new DataSeeder(_store).ResetAsync(ct);
        await RunCheckoutWithLiveProgressAsync(ct);

        CustomerDashboardService dashboardService = new(_store, failPaymentsForCustomerIds: new[] { 99 });
        CustomerDashboard dashboard = await dashboardService.LoadAsync(3, ct);
        PrintDashboard(dashboard);

        try
        {
            _ = await dashboardService.LoadAsync(99, ct);
            throw new InvalidOperationException("The injected customer and payment failures were not observed.");
        }
        catch (AggregateException ex)
        {
            foreach (Exception failure in ex.Flatten().InnerExceptions)
            {
                Console.WriteLine($"Customer #99 dashboard failure: {failure.Message}");
            }
        }

        await RunShippingQuotesAsync(ct);

        new FraudScoringService().Run(ct);

        EmailService emailService = new(_store, failuresEnabled: false);
        EmailResendService resendService = new(emailService, _store);
        await resendService.RunAsync(dashboard.Customer, ct);

        Console.WriteLine();
        Console.WriteLine("Phase 4 completed. CPU scheduling and machine load affect the measured timings and speed-up.");
    }

    private async Task RunCheckoutWithLiveProgressAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== Task 10.1: live Phase 3 checkout =====");

        IncomingOrderRepository incomingRepository = new(_store);
        List<Order> orders = await incomingRepository.GetAllAsync(ct);
        OrderProcessor processor = CreatePhase3Processor();
        Dictionary<Task<bool>, (Order Order, Stopwatch Watch)> taskOrders = new();
        Stopwatch checkoutWatch = Stopwatch.StartNew();

        foreach (Order order in orders)
        {
            Stopwatch watch = Stopwatch.StartNew();
            Task<bool> task = processor.ProcessOrderAsync(order, ct);
            taskOrders.Add(task, (order, watch));
        }

        int finished = 0;
        List<Exception> unexpectedFailures = new();
        await foreach (Task<bool> completed in Task.WhenEach(taskOrders.Keys))
        {
            (Order order, Stopwatch watch) = taskOrders[completed];
            watch.Stop();
            finished++;
            try
            {
                _ = await completed;
                string reason = string.IsNullOrWhiteSpace(order.FailureReason)
                    ? string.Empty
                    : $" ({order.FailureReason})";
                Console.WriteLine($"[{finished,2}/20] Order #{order.Id} -> {order.Status}{reason} after {watch.Elapsed.TotalSeconds:F1} s");
            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine($"[{finished,2}/20] Order #{order.Id} -> cancellation propagated after {watch.Elapsed.TotalSeconds:F1} s: {ex.Message}");
            }
            catch (Exception ex)
            {
                unexpectedFailures.Add(ex);
                Console.WriteLine($"[{finished,2}/20] Order #{order.Id} -> UNEXPECTED FAILURE after {watch.Elapsed.TotalSeconds:F1} s: {ex.Message}");
            }
        }
        checkoutWatch.Stop();
        Console.WriteLine($"All {finished} checkout operations completed in {checkoutWatch.Elapsed.TotalSeconds:F2}s.");

        List<Order> saved = await _store.ReadAllAsync<Order>(DataFiles.Orders, CancellationToken.None);
        PrintStatusCounts(saved);

        if (ct.IsCancellationRequested)
        {
            Console.WriteLine("Caller cancellation stopped outstanding Phase 3 work. Section 6.6 is unavailable, so reserved stock and a payment captured immediately before cancellation have no specified compensation/status rule.");
            throw new OperationCanceledException(ct);
        }

        if (unexpectedFailures.Count > 0)
        {
            throw new AggregateException("One or more order-processing tasks failed unexpectedly.", unexpectedFailures);
        }

        if (saved.Count != 20 || saved.Count(order => order.Status == OrderStatus.Completed) != 11 ||
            saved.Count(order => order.Status == OrderStatus.Rejected) != 6 ||
            saved.Count(order => order.Status == OrderStatus.PaymentFailed) != 2 ||
            saved.Count(order => order.Status == OrderStatus.Failed) != 1)
        {
            throw new InvalidOperationException("The Phase 3 checkout result counts did not match the Phase 3 acceptance results.");
        }

        await VerifyCheckoutFilesAsync(saved);
    }

    private OrderProcessor CreatePhase3Processor()
    {
        CustomerRepository customers = new(_store);
        ProductRepository products = new(_store);
        CouponRepository coupons = new(_store);
        return new OrderProcessor(
            new CustomerService(customers),
            new InventoryService(_store),
            new PricingService(_store, customers, products, coupons),
            new PaymentGateway(_store, new[] { 5 }, new[] { 8 }),
            new OrderProcessingSystem.Phase3.OrderRepository(_store, new[] { 13 }),
            new EmailService(_store, new Dictionary<int, int> { [3] = 2, [17] = int.MaxValue }),
            products,
            _store);
    }

    private void PrintStatusCounts(List<Order> saved)
    {
        Console.WriteLine();
        Console.WriteLine("===== Task 10.1: final status counts =====");
        foreach (OrderStatus status in Enum.GetValues<OrderStatus>())
        {
            int count = saved.Count(order => order.Status == status);
            if (count > 0)
            {
                Console.WriteLine($"{status}: {count}");
            }
        }
        Console.WriteLine($"Saved order records: {saved.Count}");
    }

    private async Task VerifyCheckoutFilesAsync(List<Order> saved)
    {
        List<PaymentTransaction> payments = await _store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, CancellationToken.None);
        List<EmailMessage> emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, CancellationToken.None);
        List<EmailMessage> retry = await _store.ReadAllAsync<EmailMessage>(DataFiles.EmailRetry, CancellationToken.None);
        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, CancellationToken.None);
        EmailMessage? queued = retry.SingleOrDefault(message => message.OrderId == 17);
        bool consistent = payments.Count == 13 &&
            payments.Count(payment => payment.Status == PaymentStatus.Captured) == 11 &&
            payments.Count(payment => payment.Status == PaymentStatus.Declined) == 1 &&
            payments.Count(payment => payment.Status == PaymentStatus.Refunded) == 1 &&
            emails.Count == 10 && retry.Count == 1 && queued?.Attempts == 4 && !string.IsNullOrWhiteSpace(queued.LastError) &&
            products.Single(product => product.Name == "Mechanical Keyboard").StockQuantity == 0;

        Console.WriteLine($"payments.json: {payments.Count} ({payments.Count(payment => payment.Status == PaymentStatus.Captured)} captured, {payments.Count(payment => payment.Status == PaymentStatus.Declined)} declined, {payments.Count(payment => payment.Status == PaymentStatus.Refunded)} refunded)");
        Console.WriteLine($"emails.json: {emails.Count}; email_retry.json: {retry.Count}; Order #17 retry attempts: {queued?.Attempts}");
        Console.WriteLine($"Mechanical Keyboard stock: {products.Single(product => product.Name == "Mechanical Keyboard").StockQuantity}");
        if (!consistent)
        {
            throw new InvalidOperationException("The Phase 3 persisted checkout results did not match the expected file state.");
        }

        foreach (Order order in saved.OrderBy(order => order.Id))
        {
            Console.WriteLine($"Order #{order.Id,-2} | {order.Status,-12} | Total: {order.TotalAmount,8:F2} | {order.FailureReason}");
        }
    }

    private static void PrintDashboard(CustomerDashboard dashboard)
    {
        string orders = string.Join(", ", dashboard.Orders.OrderBy(order => order.Id)
            .Select(order => $"#{order.Id} {order.Status}"));
        string payments = string.Join(", ", dashboard.Payments.OrderBy(payment => payment.OrderId)
            .Select(payment => $"#{payment.OrderId} {payment.Status}"));
        string emails = string.Join(", ", dashboard.Emails.OrderBy(email => email.OrderId)
            .Select(email => $"#{email.OrderId}"));
        string emailLabel = dashboard.Emails.Count == 1 ? "email" : "emails";
        Console.WriteLine($"Customer #{dashboard.Customer.Id} {dashboard.Customer.Name} — {dashboard.Orders.Count} orders ({orders}), " +
            $"{dashboard.Payments.Count} payments ({payments}), {dashboard.Emails.Count} {emailLabel} ({emails})");
    }

    private async Task RunShippingQuotesAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== Task 10.3: fastest shipping quotes =====");
        List<Order> incoming = await new IncomingOrderRepository(_store).GetAllAsync(ct);
        ShippingQuoteService quotes = CreateShippingQuoteService();
        foreach (Order order in incoming.Where(order => order.Id is >= 1 and <= 5).OrderBy(order => order.Id))
        {
            ShippingQuote quote = await quotes.GetFastestQuoteAsync(order, ct);
            string cancelled = quote.CancelledCarriers.Count == 0
                ? "none"
                : string.Join(", ", quote.CancelledCarriers);
            Console.WriteLine($"Order #{order.Id} -> {quote.Carrier}, {quote.Price:F2}, {quote.ElapsedMilliseconds} ms; cancelled: {cancelled}");
        }

        ShippingQuoteService allFailQuotes = CreateShippingQuoteService(new[] { 6 }, minimumRandomDelays: true);
        try
        {
            _ = await allFailQuotes.GetFastestQuoteAsync(incoming.Single(order => order.Id == 6), ct);
            throw new InvalidOperationException("Expected all configured carrier requests for Order #6 to fail.");
        }
        catch (ShippingProviderUnavailableException ex) when (ex.Message == "All carriers failed")
        {
            Console.WriteLine($"Order #6 all-carrier failure verified: {ex.Message}");
        }

        using CancellationTokenSource probeCancellation = new(TimeSpan.FromMilliseconds(50));
        try
        {
            _ = await quotes.GetFastestQuoteAsync(incoming.Single(order => order.Id == 7), probeCancellation.Token);
            throw new InvalidOperationException("Expected the shipping cancellation probe to be cancelled.");
        }
        catch (OperationCanceledException) when (probeCancellation.IsCancellationRequested)
        {
            Console.WriteLine("Shipping caller cancellation propagated and outstanding carrier requests were cancelled.");
        }
    }

    private ShippingQuoteService CreateShippingQuoteService(
        IEnumerable<int>? failForOrderIds = null, bool minimumRandomDelays = false) =>
        new(new IShippingCarrier[]
        {
            new FastShip(_store, failForOrderIds, minimumRandomDelays ? new MinimumDelayRandom() : null),
            new CairoExpress(_store, failForOrderIds, minimumRandomDelays ? new MinimumDelayRandom() : null),
            new NileCourier(_store, failForOrderIds, minimumRandomDelays ? new MinimumDelayRandom() : null)
        });

    private sealed class MinimumDelayRandom : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
    }
}
