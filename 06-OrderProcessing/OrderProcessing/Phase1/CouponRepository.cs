using OrderProcessingSystem.Shared.Logging;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase1;

public sealed class CouponRepository
{
    private readonly JsonFileStore _store;

    public CouponRepository(JsonFileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Log.Info("CouponRepository -> Opening connection...");
        await Task.Delay(200, ct);
        Log.Info("CouponRepository -> Executing coupon query...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Coupon> coupons = await _store.ReadAllAsync<Coupon>(DataFiles.Coupons, ct);
        Coupon? coupon = coupons.FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        Log.Info(coupon is null
            ? $"CouponRepository -> Coupon '{code}' not found"
            : $"CouponRepository -> Coupon '{code}' found");

        return coupon;
    }
}
