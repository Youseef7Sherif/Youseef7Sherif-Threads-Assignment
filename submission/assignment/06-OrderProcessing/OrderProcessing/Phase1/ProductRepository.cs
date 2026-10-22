using OrderProcessing.Shared;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class ProductRepository(JsonFileStore store)
{
    const string Source = nameof(ProductRepository);

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, $"Executing query for Product #{id}...", Latency.Between(300, 600), ct);

        List<Product> products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);
        Product? product = products.FirstOrDefault(p => p.Id == id);

        Log.Info(product is null
            ? $"{Source} -> Product #{id} not found"
            : $"{Source} -> Product #{id} found ({product.Name}, stock {product.StockQuantity})");
        return product;
    }

    public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, "Beginning transaction...", 100, ct);
        await Latency.StepAsync(Source, $"Updating stock of Product #{productId} (-{qty})...", 400, ct);

        // Check and subtract happen inside ONE locked read-modify-write
        bool reserved = await store.UpdateAsync<Product, bool>(DataFiles.Products, products =>
        {
            Product? product = products.FirstOrDefault(p => p.Id == productId);
            if (product is null || product.StockQuantity < qty)
                return false;

            product.StockQuantity -= qty;
            return true;
        }, ct);

        await Latency.StepAsync(Source, "Committing...", 200, ct);
        Log.Info($"{Source} -> Reserve Product #{productId} x{qty}: {(reserved ? "OK" : "FAILED")}");
        return reserved;
    }
}