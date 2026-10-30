using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase1;

public sealed class SyncCustomerRepository
{
    private readonly JsonFileStore _store;

    public SyncCustomerRepository(JsonFileStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Customer? GetById(int id)
    {
        Log.Info("SyncCustomerRepository -> Opening connection...");
        Thread.Sleep(200);
        Log.Info($"SyncCustomerRepository -> Executing query for Customer #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        Customer? customer = _store.ReadAll<Customer>(DataFiles.Customers)
            .FirstOrDefault(c => c.Id == id);

        Log.Info(customer is null
            ? $"SyncCustomerRepository -> Customer #{id} not found"
            : $"SyncCustomerRepository -> Customer #{id} found ({customer.Name})");
        return customer;
    }
}

public sealed class SyncProductRepository
{
    private readonly JsonFileStore _store;

    public SyncProductRepository(JsonFileStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Product? GetById(int id)
    {
        Log.Info("SyncProductRepository -> Opening connection...");
        Thread.Sleep(200);
        Log.Info($"SyncProductRepository -> Executing query for Product #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        Product? product = _store.ReadAll<Product>(DataFiles.Products)
            .FirstOrDefault(p => p.Id == id);

        Log.Info(product is null
            ? $"SyncProductRepository -> Product #{id} not found"
            : $"SyncProductRepository -> Product #{id} found ({product.Name})");
        return product;
    }
}

public sealed class SyncCouponRepository
{
    private readonly JsonFileStore _store;

    public SyncCouponRepository(JsonFileStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Coupon? GetByCode(string code)
    {
        Log.Info("SyncCouponRepository -> Opening connection...");
        Thread.Sleep(200);
        Log.Info("SyncCouponRepository -> Executing coupon query...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        Coupon? coupon = _store.ReadAll<Coupon>(DataFiles.Coupons)
            .FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        Log.Info(coupon is null
            ? $"SyncCouponRepository -> Coupon '{code}' not found"
            : $"SyncCouponRepository -> Coupon '{code}' found");
        return coupon;
    }
}
