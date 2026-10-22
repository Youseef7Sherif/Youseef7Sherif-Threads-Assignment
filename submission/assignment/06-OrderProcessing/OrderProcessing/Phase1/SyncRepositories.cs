using OrderProcessing.Shared;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class SyncCustomerRepository(JsonFileStore store)
{
    const string Source = nameof(SyncCustomerRepository);

    public Customer? GetById(int id)
    {
        Latency.Step(Source, "Opening connection...", 200);
        Latency.Step(Source, $"Executing query for Customer #{id}...", Latency.Between(300, 600));

        Customer? customer = store.ReadAll<Customer>(DataFiles.Customers).FirstOrDefault(c => c.Id == id);

        Log.Info(customer is null
            ? $"{Source} -> Customer #{id} not found"
            : $"{Source} -> Customer #{id} found ({customer.Name})");
        return customer;
    }
}

public sealed class SyncProductRepository(JsonFileStore store)
{
    const string Source = nameof(SyncProductRepository);

    public Product? GetById(int id)
    {
        Latency.Step(Source, "Opening connection...", 200);
        Latency.Step(Source, $"Executing query for Product #{id}...", Latency.Between(300, 600));

        Product? product = store.ReadAll<Product>(DataFiles.Products).FirstOrDefault(p => p.Id == id);

        Log.Info(product is null
            ? $"{Source} -> Product #{id} not found"
            : $"{Source} -> Product #{id} found ({product.Name}, stock {product.StockQuantity})");
        return product;
    }
}

public sealed class SyncCouponRepository(JsonFileStore store)
{
    const string Source = nameof(SyncCouponRepository);

    public Coupon? GetByCode(string code)
    {
        Latency.Step(Source, "Opening connection...", 200);
        Latency.Step(Source, $"Executing query for Coupon '{code}'...", Latency.Between(300, 600));

        Coupon? coupon = store.ReadAll<Coupon>(DataFiles.Coupons).FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        Log.Info(coupon is null
            ? $"{Source} -> Coupon '{code}' not found"
            : $"{Source} -> Coupon '{code}' found ({coupon.DiscountPercent}%)");
        return coupon;
    }
}