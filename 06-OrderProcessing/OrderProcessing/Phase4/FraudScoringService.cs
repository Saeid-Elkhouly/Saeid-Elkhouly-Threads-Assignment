using System.Collections.Concurrent;
using System.Diagnostics;
using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase4;

public sealed class FraudScoringService
{
    public void Run(CancellationToken ct)
    {
        List<Order> orders = Enumerable.Range(1, 10_000)
            .Select(id => new Order { Id = id })
            .ToList();

        List<int> sequentialFlagged = new();
        Stopwatch sequentialWatch = Stopwatch.StartNew();
        foreach (Order order in orders)
        {
            ct.ThrowIfCancellationRequested();
            if (Score(order.Id) >= 90)
            {
                sequentialFlagged.Add(order.Id);
            }
        }
        sequentialWatch.Stop();

        int[] parallelFlaggedByIndex = new int[orders.Count];
        ConcurrentDictionary<int, byte> workerThreads = new();
        Stopwatch parallelWatch = Stopwatch.StartNew();
        Parallel.ForEach(orders, new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, order =>
        {
            workerThreads.TryAdd(Environment.CurrentManagedThreadId, 0);
            parallelFlaggedByIndex[order.Id - 1] = Score(order.Id) >= 90 ? order.Id : 0;
        });
        parallelWatch.Stop();

        List<int> parallelFlagged = parallelFlaggedByIndex.Where(id => id != 0).ToList();
        bool sameIds = sequentialFlagged.SequenceEqual(parallelFlagged);
        int[] firstFive = parallelFlagged.Take(5).ToArray();
        if (!sameIds || sequentialFlagged.Count != 1005 || !firstFive.SequenceEqual(new[] { 4, 8, 12, 29, 33 }))
        {
            throw new InvalidOperationException(
                $"Fraud scoring verification failed. Sequential={sequentialFlagged.Count}, " +
                $"Parallel={parallelFlagged.Count}, First={string.Join(", ", firstFive)}, sameIds={sameIds}.");
        }

        Console.WriteLine();
        Console.WriteLine("===== CPU-bound fraud scoring =====");
        Console.WriteLine($"Flagged orders: {parallelFlagged.Count}");
        Console.WriteLine($"Sequential elapsed: {sequentialWatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Parallel elapsed:   {parallelWatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Speed-up: {sequentialWatch.Elapsed.TotalSeconds / parallelWatch.Elapsed.TotalSeconds:F2}x");
        Console.WriteLine($"Parallel worker threads: {workerThreads.Count}");
        Console.WriteLine($"Environment.ProcessorCount: {Environment.ProcessorCount}");
        Console.WriteLine($"First five flagged IDs: {string.Join(", ", firstFive)}");
    }

    private static int Score(int orderId)
    {
        long h = orderId;

        for (int i = 0; i < 100_000; i++)
        {
            h = (h * 31 + i) % 1_000_000_007;
        }

        return (int)(h % 100);
    }
}
