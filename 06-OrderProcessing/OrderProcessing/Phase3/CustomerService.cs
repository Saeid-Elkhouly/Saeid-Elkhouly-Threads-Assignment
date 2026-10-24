using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Models;

namespace OrderProcessingSystem.Phase3;

public sealed class CustomerService
{
    private readonly CustomerRepository _customers;

    public CustomerService(CustomerRepository customers) =>
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));

    public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct)
    {
        Customer? customer = await _customers.GetByIdAsync(id, ct);
        return customer ?? throw new CustomerNotFoundException($"Customer #{id} not found");
    }

    public Task<Customer> GetCustomerAsync(int id) => GetCustomerAsync(id, CancellationToken.None);
}
