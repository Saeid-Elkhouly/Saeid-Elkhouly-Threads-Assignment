using OrderProcessingSystem.Phase2;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase4;

public sealed class FastShip : ShippingCarrierBase
{
    public FastShip(JsonFileStore store, IEnumerable<int>? failForOrderIds = null, Random? random = null)
        : base("FastShip", store, 500, 1500, 1.00m, failForOrderIds, random)
    {
    }
}

public sealed class CairoExpress : ShippingCarrierBase
{
    public CairoExpress(JsonFileStore store, IEnumerable<int>? failForOrderIds = null, Random? random = null)
        : base("CairoExpress", store, 300, 2000, 0.95m, failForOrderIds, random, failEvenOrderIds: true)
    {
    }
}

public sealed class NileCourier : ShippingCarrierBase
{
    public NileCourier(JsonFileStore store, IEnumerable<int>? failForOrderIds = null, Random? random = null)
        : base("NileCourier", store, 800, 1200, 1.10m, failForOrderIds, random)
    {
    }
}

public abstract class ShippingCarrierBase : IShippingCarrier
{
    private readonly JsonFileStore _store;
    private readonly int _minimumWaitMilliseconds;
    private readonly int _maximumWaitMilliseconds;
    private readonly decimal _priceFactor;
    private readonly HashSet<int> _failForOrderIds;
    private readonly Random? _random;
    private readonly bool _failEvenOrderIds;

    protected ShippingCarrierBase(string name, JsonFileStore store, int minimumWaitMilliseconds,
        int maximumWaitMilliseconds, decimal priceFactor, IEnumerable<int>? failForOrderIds,
        Random? random, bool failEvenOrderIds = false)
    {
        Name = name;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _minimumWaitMilliseconds = minimumWaitMilliseconds;
        _maximumWaitMilliseconds = maximumWaitMilliseconds;
        _priceFactor = priceFactor;
        _failForOrderIds = new HashSet<int>(failForOrderIds ?? Array.Empty<int>());
        _random = random;
        _failEvenOrderIds = failEvenOrderIds;
    }

    public string Name { get; }

    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        await Task.Delay(150, ct);

        Random random = _random ?? Random.Shared;
        await Task.Delay(random.Next(_minimumWaitMilliseconds, _maximumWaitMilliseconds + 1), ct);

        if (_failForOrderIds.Contains(order.Id))
        {
            throw new ShippingProviderUnavailableException($"{Name} is unavailable for Order #{order.Id}");
        }

        if (_failEvenOrderIds && order.Id % 2 == 0)
        {
            throw new ShippingProviderUnavailableException($"{Name} is unavailable for even Order #{order.Id}");
        }

        decimal basePrice = await CalculateBasePriceAsync(order, ct);
        return PricingService.RoundMoney(basePrice * _priceFactor);
    }

    private async Task<decimal> CalculateBasePriceAsync(Order order, CancellationToken ct)
    {
        List<Product> products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);
        decimal subtotal = 0m;
        int totalQuantity = 0;
        decimal totalWeight = 0m;

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();
            Product product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Product #{item.ProductId} not found while quoting Order #{order.Id}.");
            subtotal += product.Price * item.Quantity;
            totalQuantity += item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
        }

        decimal shipping = subtotal >= 1000m ? 0m : 5m + (1.50m * totalQuantity) + (2m * totalWeight);
        return PricingService.RoundMoney(shipping);
    }
}
