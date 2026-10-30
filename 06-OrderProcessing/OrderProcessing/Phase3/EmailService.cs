using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Services;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class EmailService : IEmailService
{
    private readonly JsonFileStore _store;
    private readonly IReadOnlyDictionary<int, int> _failAttempts;
    private readonly bool _failuresEnabled;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _attempts = new();

    public EmailService(JsonFileStore store, IReadOnlyDictionary<int, int>? failAttempts = null,
        bool failuresEnabled = true)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _failAttempts = failAttempts ?? new Dictionary<int, int>();
        _failuresEnabled = failuresEnabled;
    }

    public async Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(customer);
        int attempt = _attempts.AddOrUpdate(order.Id, 1, static (_, previous) => previous + 1);
        Log.Info($"EmailService -> Sending confirmation for Order #{order.Id}, attempt {attempt}...");
        await Task.Delay(500, ct);

        if (_failuresEnabled && _failAttempts.TryGetValue(order.Id, out int maxFailures) && attempt <= maxFailures)
        {
            throw new SmtpUnavailableException("SMTP server unavailable");
        }

        EmailMessage email = new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmation",
            Body = $"Your order is confirmed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = attempt
        };
        await _store.UpdateAsync<EmailMessage>(DataFiles.Emails, emails =>
        {
            emails.Add(email);
            return emails;
        }, ct);
        Log.Success($"EmailService -> Confirmation for Order #{order.Id} sent.");
    }

    public Task SendConfirmationAsync(Order order, Customer customer) =>
        SendConfirmationAsync(order, customer, CancellationToken.None);
}
