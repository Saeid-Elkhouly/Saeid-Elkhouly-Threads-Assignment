using System.Collections.Concurrent;
using System.Diagnostics;
using OrderProcessingSystem.Phase3;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Services;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase4;

public sealed class EmailResendService
{
    private const int MaximumConcurrency = 4;
    private readonly IEmailService _emailService;
    private readonly JsonFileStore _store;

    public EmailResendService(IEmailService emailService, JsonFileStore store)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task RunAsync(Customer customer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(customer);
        int[] orderIds = Enumerable.Range(1001, 30).ToArray();
        HashSet<int> testOrderIds = orderIds.ToHashSet();
        List<EmailMessage> priorEmails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        int[] existingTestIds = priorEmails.Where(email => testOrderIds.Contains(email.OrderId))
            .Select(email => email.OrderId).Distinct().ToArray();
        if (existingTestIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Email resend test data already exists for order IDs: {string.Join(", ", existingTestIds)}.");
        }

        int activeSends = 0;
        int maximumActiveSends = 0;
        Stopwatch watch = Stopwatch.StartNew();
        await Parallel.ForEachAsync(orderIds, new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = MaximumConcurrency
        }, async (orderId, token) =>
        {
            int active = Interlocked.Increment(ref activeSends);
            int observed;
            do
            {
                observed = Volatile.Read(ref maximumActiveSends);
                if (active <= observed)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref maximumActiveSends, active, observed) != observed);

            try
            {
                Order order = new()
                {
                    Id = orderId,
                    CustomerId = customer.Id,
                    Status = OrderStatus.Completed
                };
                await _emailService.SendConfirmationAsync(order, customer, token);
            }
            finally
            {
                Interlocked.Decrement(ref activeSends);
            }
        });
        watch.Stop();

        List<EmailMessage> emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        List<int> persistedTestIds = emails.Where(email => testOrderIds.Contains(email.OrderId))
            .Select(email => email.OrderId).ToList();
        bool exactOnce = persistedTestIds.Count == orderIds.Length &&
            orderIds.All(id => persistedTestIds.Count(value => value == id) == 1);
        if (!exactOnce)
        {
            throw new InvalidOperationException("The 30 resend emails were not persisted exactly once each.");
        }

        if (maximumActiveSends != MaximumConcurrency)
        {
            throw new InvalidOperationException(
                $"Expected exactly {MaximumConcurrency} overlapping email sends; observed {maximumActiveSends}.");
        }

        Console.WriteLine();
        Console.WriteLine("===== I/O-bound email re-send =====");
        Console.WriteLine($"Persisted {persistedTestIds.Count} distinct test emails exactly once.");
        Console.WriteLine($"Maximum simultaneous sends: {maximumActiveSends}");
        Console.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"emails.json total records: {emails.Count}");
    }
}
