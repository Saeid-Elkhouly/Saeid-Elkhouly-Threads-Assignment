using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Services;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class PaymentGateway : IPaymentGateway
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;
    private readonly bool _failuresEnabled;

    public PaymentGateway(JsonFileStore store, IEnumerable<int>? timeoutOrderIds = null,
        IEnumerable<int>? declineOrderIds = null, bool failuresEnabled = true)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeoutOrderIds = new HashSet<int>(timeoutOrderIds ?? Array.Empty<int>());
        _declineOrderIds = new HashSet<int>(declineOrderIds ?? Array.Empty<int>());
        _failuresEnabled = failuresEnabled;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct)
    {
        Log.Info($"PaymentGateway -> Fraud check for Order #{orderId}...");
        await Task.Delay(200, ct);
        bool declined = _failuresEnabled && _declineOrderIds.Contains(orderId);
        if (!declined)
        {
            Log.Info($"PaymentGateway -> Processing payment for Order #{orderId}...");
            await Task.Delay(_failuresEnabled && _timeoutOrderIds.Contains(orderId) ? 5000 : 700, ct);
        }

        string? transactionId = declined ? null : $"TXN-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        PaymentResult result = new()
        {
            Success = !declined,
            TransactionId = transactionId,
            Status = declined ? PaymentStatus.Declined : PaymentStatus.Captured,
            Message = declined ? "Card declined by issuer" : null
        };
        PaymentTransaction transaction = new()
        {
            OrderId = orderId,
            Amount = amount,
            Status = result.Status,
            TransactionId = transactionId,
            CreatedAt = DateTime.Now
        };

        await _store.UpdateAsync<PaymentTransaction>(DataFiles.Payments, records =>
        {
            records.Add(transaction);
            return records;
        }, ct);
        return result;
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        Log.Info($"PaymentGateway -> Refunding {transactionId}...");
        await Task.Delay(700, ct);
        await _store.UpdateAsync<PaymentTransaction>(DataFiles.Payments, records =>
        {
            PaymentTransaction transaction = records.FirstOrDefault(record => record.TransactionId == transactionId)
                ?? throw new InvalidOperationException($"Payment transaction '{transactionId}' was not found.");
            transaction.Status = PaymentStatus.Refunded;
            return records;
        }, ct);
    }

    public Task RefundAsync(string transactionId) => RefundAsync(transactionId, CancellationToken.None);
}
