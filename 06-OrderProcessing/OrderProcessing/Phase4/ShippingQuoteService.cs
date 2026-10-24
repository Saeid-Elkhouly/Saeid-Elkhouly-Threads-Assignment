using System.Diagnostics;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase4;

public sealed class ShippingQuoteService
{
    private static readonly TimeSpan TimeBudget = TimeSpan.FromMilliseconds(1200);
    private readonly IReadOnlyList<IShippingCarrier> _carriers;

    public ShippingQuoteService(IEnumerable<IShippingCarrier> carriers)
    {
        _carriers = carriers?.ToList() ?? throw new ArgumentNullException(nameof(carriers));
        if (_carriers.Count != 3)
        {
            throw new ArgumentException("Exactly three shipping carriers are required.", nameof(carriers));
        }
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        Stopwatch watch = Stopwatch.StartNew();
        using CancellationTokenSource timeout = new(TimeBudget);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        Task<CarrierAttempt>[] attempts = _carriers.Select(carrier => RunCarrierAsync(carrier, order, linked.Token)).ToArray();
        int failures = 0;

        try
        {
            await foreach (Task<CarrierAttempt> completed in Task.WhenEach(attempts))
            {
                CarrierAttempt attempt = await completed;
                if (ct.IsCancellationRequested)
                {
                    linked.Cancel();
                    await Task.WhenAll(attempts);
                    throw new OperationCanceledException(ct);
                }

                if (timeout.IsCancellationRequested)
                {
                    linked.Cancel();
                    List<string> cancelled = await CancelAndObserveAsync(attempts);
                    watch.Stop();
                    Log.Info($"Order #{order.Id} shipping time budget expired; using flat rate.");
                    return new ShippingQuote
                    {
                        Carrier = "Flat rate",
                        Price = 25.00m,
                        ElapsedMilliseconds = watch.ElapsedMilliseconds,
                        CancelledCarriers = cancelled
                    };
                }

                if (attempt.Price.HasValue)
                {
                    linked.Cancel();
                    List<string> cancelled = await CancelAndObserveAsync(attempts);
                    watch.Stop();
                    return new ShippingQuote
                    {
                        Carrier = attempt.Carrier.Name,
                        Price = attempt.Price.Value,
                        ElapsedMilliseconds = watch.ElapsedMilliseconds,
                        CancelledCarriers = cancelled
                    };
                }

                if (attempt.Error is not null)
                {
                    failures++;
                    Log.Error($"[{attempt.Carrier.Name}] quote failed for Order #{order.Id}: {attempt.Error.Message}");
                    if (failures == attempts.Length)
                    {
                        watch.Stop();
                        throw new ShippingProviderUnavailableException("All carriers failed");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            linked.Cancel();
            await CancelAndObserveAsync(attempts);
            throw;
        }

        linked.Cancel();
        List<string> outstanding = await CancelAndObserveAsync(attempts);
        watch.Stop();
        if (timeout.IsCancellationRequested)
        {
            return new ShippingQuote
            {
                Carrier = "Flat rate",
                Price = 25.00m,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                CancelledCarriers = outstanding
            };
        }

        throw new ShippingProviderUnavailableException("All carriers failed");
    }

    private static async Task<CarrierAttempt> RunCarrierAsync(
        IShippingCarrier carrier, Order order, CancellationToken ct)
    {
        try
        {
            decimal price = await carrier.GetQuoteAsync(order, ct);
            return new CarrierAttempt(carrier, price, null, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new CarrierAttempt(carrier, null, null, true);
        }
        catch (Exception ex)
        {
            return new CarrierAttempt(carrier, null, ex, false);
        }
    }

    private static async Task<List<string>> CancelAndObserveAsync(Task<CarrierAttempt>[] attempts)
    {
        CarrierAttempt[] results = await Task.WhenAll(attempts);
        List<string> cancelled = results.Where(result => result.WasCancelled)
            .Select(result => result.Carrier.Name).ToList();
        foreach (string carrier in cancelled)
        {
            Log.Info($"[{carrier}] cancelled");
        }

        return cancelled;
    }

    private sealed record CarrierAttempt(
        IShippingCarrier Carrier,
        decimal? Price,
        Exception? Error,
        bool WasCancelled);
}
