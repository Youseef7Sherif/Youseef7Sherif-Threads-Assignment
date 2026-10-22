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

        await CommitAsync(productId, reserved ? -qty : 0, ct);
        Log.Info($"{Source} -> Reserve Product #{productId} x{qty}: {(reserved ? "OK" : "FAILED")}");
        return reserved;
    }

    public async Task ReleaseStockAsync(int productId, int qty, CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, "Beginning transaction...", 100, ct);
        await Latency.StepAsync(Source, $"Updating stock of Product #{productId} (+{qty})...", 400, ct);

        await store.UpdateAsync<Product>(DataFiles.Products, products =>
        {
            Product? product = products.FirstOrDefault(p => p.Id == productId);
            if (product is not null)
                product.StockQuantity += qty;
        }, ct);

        await CommitAsync(productId, qty, ct);
        Log.Info($"{Source} -> Released Product #{productId} x{qty}");
    }

    public async Task<List<Product>> GetAllAsync(CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, "Executing query for all products...", Latency.Between(300, 600), ct);

        List<Product> products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);
        Log.Info($"{Source} -> {products.Count} products returned");
        return products;
    }

    // UNSAFE ON PURPOSE (race demo): read ... wait ... write, with no lock around the three steps
    public async Task<bool> ReserveStockUnsafeAsync(int productId, int qty, CancellationToken ct)
    {
        List<Product> products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);   // 1) read
        Product? product = products.FirstOrDefault(p => p.Id == productId);
        if (product is null || product.StockQuantity < qty)
            return false;

        int newStock = product.StockQuantity - qty;

        await Latency.StepAsync(Source, "Opening connection...", 200, ct);                    // 2) wait
        await Latency.StepAsync(Source, "Beginning transaction...", 100, ct);
        await Latency.StepAsync(Source, $"UNSAFE: updating stock of Product #{productId}...", 400, ct);

        product.StockQuantity = newStock;
        await store.WriteAllAsync(DataFiles.Products, products, ct);                          // 3) write an old copy

        await Latency.StepAsync(Source, "Committing...", 200, ct);
        return true;
    }

    // If the run is cancelled during Commit, undo the change that was already written,
    // so a reserve/release is all-or-nothing and no stock is lost or counted twice
    async Task CommitAsync(int productId, int appliedChange, CancellationToken ct)
    {
        try
        {
            await Latency.StepAsync(Source, "Committing...", 200, ct);
        }
        catch (OperationCanceledException)
        {
            await store.UpdateAsync<Product>(DataFiles.Products, products =>
            {
                Product? product = products.FirstOrDefault(p => p.Id == productId);
                if (product is not null)
                    product.StockQuantity -= appliedChange;
            }, CancellationToken.None);
            throw;
        }
    }
}