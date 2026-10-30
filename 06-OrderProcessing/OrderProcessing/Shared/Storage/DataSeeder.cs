

using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Storage;

public sealed class DataSeeder
{
    private readonly JsonFileStore _store;
    public DataSeeder(JsonFileStore store) => _store = store;

    public async Task ResetAsync(CancellationToken ct)
    {
        string directory = _store.DataDirectory;
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        Directory.CreateDirectory(directory);

        List<Customer> customers = new()
        {
            new() { Id=1, Name="Ahmed Ali", Email="ahmed.ali@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Gold },
            new() { Id=2, Name="Sara Mohamed", Email="sara.mohamed@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Silver },
            new() { Id=3, Name="Omar Hassan", Email="omar.hassan@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Bronze },
            new() { Id=4, Name="Mona Ibrahim", Email="mona.ibrahim@example.com", IsActive=false, LoyaltyTier=LoyaltyTier.Silver },
            new() { Id=5, Name="Youssef Adel", Email="youssef.adel@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Gold },
            new() { Id=6, Name="Nour Khaled", Email="nour.khaled@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Bronze },
            new() { Id=7, Name="Karim Samir", Email="karim.samir@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Silver },
            new() { Id=8, Name="Laila Mahmoud", Email="laila.mahmoud@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Gold },
            new() { Id=9, Name="Hassan Tarek", Email="hassan.tarek@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Bronze },
            new() { Id=10, Name="Dina Fathy", Email="dina.fathy@example.com", IsActive=true, LoyaltyTier=LoyaltyTier.Silver }
        };
        List<Product> products = new()
        {
            new() { Id=1, Name="Laptop", Price=1200m, StockQuantity=10, WeightKg=2.2m },
            new() { Id=2, Name="Smartphone", Price=800m, StockQuantity=15, WeightKg=0.4m },
            new() { Id=3, Name="Wireless Mouse", Price=25m, StockQuantity=2, WeightKg=0.1m },
            new() { Id=4, Name="Mechanical Keyboard", Price=90m, StockQuantity=1, WeightKg=0.9m },
            new() { Id=5, Name="USB-C Hub", Price=45m, StockQuantity=30, WeightKg=0.2m },
            new() { Id=6, Name="Headphones", Price=150m, StockQuantity=12, WeightKg=0.3m },
            new() { Id=7, Name="27\" Monitor", Price=350m, StockQuantity=6, WeightKg=5.0m },
            new() { Id=8, Name="Webcam", Price=70m, StockQuantity=20, WeightKg=0.2m }
        };
        List<Coupon> coupons = new()
        {
            new() { Code="SAVE10", DiscountPercent=10, ExpiryDate=DateTime.Today.AddDays(180) },
            new() { Code="WELCOME15", DiscountPercent=15, ExpiryDate=DateTime.Today.AddDays(90) },
            new() { Code="FLASH20", DiscountPercent=20, ExpiryDate=DateTime.Today.AddDays(7) },
            new() { Code="OLD20", DiscountPercent=20, ExpiryDate=DateTime.Today.AddDays(-30) }
        };
        List<Order> orders = new()
        {
            MakeOrder(1,1,(1,1),(3,1),"SAVE10"), MakeOrder(2,2,(2,1)),
            MakeOrder(3,3,(5,2),(8,1),"WELCOME15"), MakeOrder(4,4,(6,1)),
            MakeOrder(5,5,(7,1)), MakeOrder(6,6,(5,1),coupon:"FLASH20"),
            MakeOrder(7,7,(6,1),(8,1)), MakeOrder(8,8,(1,1),coupon:"SAVE10"),
            MakeOrder(9,9,(3,5)), MakeOrder(10,10,(4,1)), MakeOrder(11,1,(4,1)),
            MakeOrder(12,2,(2,1),coupon:"OLD20"), MakeOrder(13,3,(6,2)),
            MakeOrder(14,5,(8,2),(5,1),"WELCOME15"), MakeOrder(15,99,(2,1)),
            MakeOrder(16,6,(7,1),(5,1)), MakeOrder(17,7,(2,1),coupon:"SAVE10"),
            MakeOrder(18,8,(6,1),coupon:"FAKE50"), MakeOrder(19,9,(8,1)),
            MakeOrder(20,10,(1,1),(6,1),"FLASH20")
        };

        await WriteAndLog(DataFiles.Customers, customers, ct);
        await WriteAndLog(DataFiles.Products, products, ct);
        await WriteAndLog(DataFiles.Coupons, coupons, ct);
        await WriteAndLog(DataFiles.IncomingOrders, orders, ct);
        await WriteAndLog(DataFiles.Orders, new List<Order>(), ct);
        await WriteAndLog(DataFiles.Payments, new List<PaymentTransaction>(), ct);
        await WriteAndLog(DataFiles.Emails, new List<EmailMessage>(), ct);
        await WriteAndLog(DataFiles.EmailRetry, new List<EmailMessage>(), ct);
        Directory.CreateDirectory(Path.Combine(directory, DataFiles.InvoicesDirectory));
        Directory.CreateDirectory(Path.Combine(directory, DataFiles.ReportsDirectory));
    }

    private async Task WriteAndLog<T>(string fileName, List<T> records, CancellationToken ct)
    {
        await _store.WriteAllAsync(fileName, records, ct);
        LogLine(fileName, records.Count);
    }

    private static void LogLine(string fileName, int count) => Console.WriteLine($"[Seeder] {fileName} -> {count} records");

    private static Order MakeOrder(int id, int customerId, (int productId, int qty) item1,
        (int productId, int qty)? item2 = null, string? coupon = null)
    {
        List<OrderItem> items = new() { new() { ProductId=item1.productId, Quantity=item1.qty, UnitPrice=0m } };
        if (item2.HasValue) items.Add(new OrderItem { ProductId=item2.Value.productId, Quantity=item2.Value.qty, UnitPrice=0m });
        return new Order { Id=id, CustomerId=customerId, Items=items, CouponCode=coupon, Status=OrderStatus.Pending,
            CreatedAt=DateTime.Now, SubTotal=0m, Discount=0m, ShippingCost=0m, TotalAmount=0m, FailureReason=null, TransactionId=null };
    }
}
