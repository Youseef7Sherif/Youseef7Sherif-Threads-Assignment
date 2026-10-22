using OrderProcessing.Shared;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class IncomingOrderRepository(JsonFileStore store)
{
    const string Source = nameof(IncomingOrderRepository);

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, "Executing query for incoming orders...", Latency.Between(300, 600), ct);

        List<Order> orders = await store.ReadAllAsync<Order>(DataFiles.IncomingOrders, ct);
        Log.Info($"{Source} -> {orders.Count} incoming orders returned");
        return orders;
    }
}