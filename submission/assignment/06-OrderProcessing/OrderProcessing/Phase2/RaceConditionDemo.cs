using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class RaceConditionDemo(JsonFileStore store, ProductRepository products)
{
    const int KeyboardId = 4;

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("--- Race demo: the last Mechanical Keyboard (orders #10 and #11) ---");

        // UNSAFE: both orders start before either one has written
        await SetKeyboardStockAsync(1, ct);
        Task<bool> unsafe10 = products.ReserveStockUnsafeAsync(KeyboardId, 1, ct);
        Task<bool> unsafe11 = products.ReserveStockUnsafeAsync(KeyboardId, 1, ct);
        bool unsafeResult10 = await unsafe10;
        bool unsafeResult11 = await unsafe11;
        Product? afterUnsafe = await products.GetByIdAsync(KeyboardId, ct);
        Console.WriteLine($"UNSAFE: order #10 -> {unsafeResult10}, order #11 -> {unsafeResult11}, stock in file = {afterUnsafe?.StockQuantity}");

        // SAFE: the same two orders again
        await SetKeyboardStockAsync(1, ct);
        Task<bool> safe10 = products.ReserveStockAsync(KeyboardId, 1, ct);
        Task<bool> safe11 = products.ReserveStockAsync(KeyboardId, 1, ct);
        bool safeResult10 = await safe10;
        bool safeResult11 = await safe11;
        Product? afterSafe = await products.GetByIdAsync(KeyboardId, ct);
        Console.WriteLine($"SAFE:   order #10 -> {safeResult10}, order #11 -> {safeResult11}, stock in file = {afterSafe?.StockQuantity}");
    }

    Task SetKeyboardStockAsync(int stock, CancellationToken ct) =>
        store.UpdateAsync<Product>(DataFiles.Products, list =>
        {
            list.First(p => p.Id == KeyboardId).StockQuantity = stock;
        }, ct);
}