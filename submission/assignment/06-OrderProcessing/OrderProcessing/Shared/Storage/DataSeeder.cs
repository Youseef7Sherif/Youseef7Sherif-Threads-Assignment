using OrderProcessing.Shared.Models;

namespace OrderProcessing.Shared.Storage;

public static class DataSeeder
{
    public static async Task ResetAsync(CancellationToken ct = default)
    {
        if (Directory.Exists(DataFiles.Folder))
            Directory.Delete(DataFiles.Folder, recursive: true);

        Directory.CreateDirectory(DataFiles.Folder);
        Directory.CreateDirectory(DataFiles.InvoicesFolder);
        Directory.CreateDirectory(DataFiles.ReportsFolder);

        var store = new JsonFileStore();

        await SeedAsync(store, DataFiles.Customers, BuildCustomers(), ct);
        await SeedAsync(store, DataFiles.Products, BuildProducts(), ct);
        await SeedAsync(store, DataFiles.Coupons, BuildCoupons(), ct);
        await SeedAsync(store, DataFiles.IncomingOrders, BuildIncomingOrders(), ct);
        await SeedAsync(store, DataFiles.Orders, new List<Order>(), ct);
        await SeedAsync(store, DataFiles.Payments, new List<PaymentTransaction>(), ct);
        await SeedAsync(store, DataFiles.Emails, new List<EmailMessage>(), ct);
        await SeedAsync(store, DataFiles.EmailRetry, new List<EmailMessage>(), ct);
    }

    static async Task SeedAsync<T>(JsonFileStore store, string fileName, List<T> items, CancellationToken ct)
    {
        await store.WriteAllAsync(fileName, items, ct);
        Console.WriteLine($"[Seeder] {fileName} -> {items.Count} records.");
    }

    // ---------- seed data (exact values from the assignment) ----------

    static List<Customer> BuildCustomers() =>
    [
        Cust(1, "Ahmed Ali", true, LoyaltyTier.Gold),
        Cust(2, "Sara Mohamed", true, LoyaltyTier.Silver),
        Cust(3, "Omar Hassan", true, LoyaltyTier.Bronze),
        Cust(4, "Mona Ibrahim", false, LoyaltyTier.Silver),
        Cust(5, "Youssef Adel", true, LoyaltyTier.Gold),
        Cust(6, "Nour Khaled", true, LoyaltyTier.Bronze),
        Cust(7, "Karim Samir", true, LoyaltyTier.Silver),
        Cust(8, "Laila Mahmoud", true, LoyaltyTier.Gold),
        Cust(9, "Hassan Tarek", true, LoyaltyTier.Bronze),
        Cust(10, "Dina Fathy", true, LoyaltyTier.Silver),
    ];

    static List<Product> BuildProducts() =>
    [
        Prod(1, "Laptop", 1200.00m, 10, 2.2m),
        Prod(2, "Smartphone", 800.00m, 15, 0.4m),
        Prod(3, "Wireless Mouse", 25.00m, 2, 0.1m),
        Prod(4, "Mechanical Keyboard", 90.00m, 1, 0.9m),
        Prod(5, "USB-C Hub", 45.00m, 30, 0.2m),
        Prod(6, "Headphones", 150.00m, 12, 0.3m),
        Prod(7, "27\" Monitor", 350.00m, 6, 5.0m),
        Prod(8, "Webcam", 70.00m, 20, 0.2m),
    ];

    static List<Coupon> BuildCoupons() =>
    [
        new() { Code = "SAVE10", DiscountPercent = 10, ExpiryDate = DateTime.Today.AddDays(180) },
        new() { Code = "WELCOME15", DiscountPercent = 15, ExpiryDate = DateTime.Today.AddDays(90) },
        new() { Code = "FLASH20", DiscountPercent = 20, ExpiryDate = DateTime.Today.AddDays(7) },
        new() { Code = "OLD20", DiscountPercent = 20, ExpiryDate = DateTime.Today.AddDays(-30) },
    ];

    static List<Order> BuildIncomingOrders() =>
    [
        Ord(1, 1, "SAVE10", (1, 1), (3, 1)),
        Ord(2, 2, null, (2, 1)),
        Ord(3, 3, "WELCOME15", (5, 2), (8, 1)),
        Ord(4, 4, null, (6, 1)),
        Ord(5, 5, null, (7, 1)),
        Ord(6, 6, "FLASH20", (5, 1)),
        Ord(7, 7, null, (6, 1), (8, 1)),
        Ord(8, 8, "SAVE10", (1, 1)),
        Ord(9, 9, null, (3, 5)),
        Ord(10, 10, null, (4, 1)),
        Ord(11, 1, null, (4, 1)),
        Ord(12, 2, "OLD20", (2, 1)),
        Ord(13, 3, null, (6, 2)),
        Ord(14, 5, "WELCOME15", (8, 2), (5, 1)),
        Ord(15, 99, null, (2, 1)),
        Ord(16, 6, null, (7, 1), (5, 1)),
        Ord(17, 7, "SAVE10", (2, 1)),
        Ord(18, 8, "FAKE50", (6, 1)),
        Ord(19, 9, null, (8, 1)),
        Ord(20, 10, "FLASH20", (1, 1), (6, 1)),
    ];

    static Customer Cust(int id, string name, bool active, LoyaltyTier tier) => new()
    {
        Id = id,
        Name = name,
        Email = name.ToLowerInvariant().Replace(' ', '.') + "@example.com",
        IsActive = active,
        LoyaltyTier = tier
    };

    static Product Prod(int id, string name, decimal price, int stock, decimal weightKg) => new()
    {
        Id = id,
        Name = name,
        Price = price,
        StockQuantity = stock,
        WeightKg = weightKg
    };

    static Order Ord(int id, int customerId, string? coupon, params (int ProductId, int Quantity)[] items) => new()
    {
        Id = id,
        CustomerId = customerId,
        CouponCode = coupon,
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.Now,
        FailureReason = null,
        Items = items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPrice = 0
        }).ToList()
    };
}