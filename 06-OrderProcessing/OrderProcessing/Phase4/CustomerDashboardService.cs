using System.Diagnostics;
using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase4;

public sealed class CustomerDashboardService
{
    private const int SimulatedLoadMilliseconds = 700;
    private readonly JsonFileStore _store;
    private readonly CustomerRepository _customers;
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(JsonFileStore store, IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _customers = new CustomerRepository(store);
        _failPaymentsForCustomerIds = new HashSet<int>(failPaymentsForCustomerIds ?? Array.Empty<int>());
    }

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct)
    {
        Stopwatch watch = Stopwatch.StartNew();
        Task<Customer> customerTask = LoadCustomerAsync(customerId, ct);
        Task<List<Order>> ordersTask = LoadOrdersAsync(customerId, ct);
        Task<List<PaymentTransaction>> paymentsTask = LoadPaymentsAsync(customerId, ct);
        Task<List<EmailMessage>> emailsTask = LoadEmailsAsync(customerId, ct);
        Task allLoads = Task.WhenAll(customerTask, ordersTask, paymentsTask, emailsTask);

        try
        {
            await allLoads;
        }
        catch
        {
            watch.Stop();
            Log.Info($"Customer dashboard #{customerId} loads finished in {watch.ElapsedMilliseconds} ms.");
            AggregateException? failures = allLoads.Exception?.Flatten();
            if (failures is null)
            {
                throw;
            }

            foreach (Exception exception in failures.InnerExceptions)
            {
                Log.Error($"Customer dashboard #{customerId} load failed: {exception.Message}");
            }

            throw new AggregateException(failures.InnerExceptions);
        }

        watch.Stop();
        Log.Info($"Customer dashboard #{customerId} loads finished in {watch.ElapsedMilliseconds} ms.");
        return new CustomerDashboard
        {
            Customer = customerTask.Result,
            Orders = ordersTask.Result,
            Payments = paymentsTask.Result,
            Emails = emailsTask.Result
        };
    }

    private async Task<Customer> LoadCustomerAsync(int customerId, CancellationToken ct)
    {
        Customer? customer = await _customers.GetByIdAsync(customerId, ct);
        return customer ?? throw new CustomerNotFoundException($"Customer #{customerId} not found");
    }

    private async Task<List<Order>> LoadOrdersAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(SimulatedLoadMilliseconds, ct);
        List<Order> orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        return orders.Where(order => order.CustomerId == customerId).ToList();
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(SimulatedLoadMilliseconds, ct);
        if (_failPaymentsForCustomerIds.Contains(customerId))
        {
            throw new TimeoutException("Payments store timeout");
        }

        List<Order> orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        HashSet<int> orderIds = orders.Where(order => order.CustomerId == customerId)
            .Select(order => order.Id).ToHashSet();
        List<PaymentTransaction> payments = await _store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, ct);
        return payments.Where(payment => orderIds.Contains(payment.OrderId)).ToList();
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(SimulatedLoadMilliseconds, ct);
        List<Order> orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        HashSet<int> orderIds = orders.Where(order => order.CustomerId == customerId)
            .Select(order => order.Id).ToHashSet();
        List<EmailMessage> emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        return emails.Where(email => orderIds.Contains(email.OrderId)).ToList();
    }
}
