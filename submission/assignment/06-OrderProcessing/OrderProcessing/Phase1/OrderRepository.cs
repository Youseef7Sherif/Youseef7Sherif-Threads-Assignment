using OrderProcessing.Shared;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class OrderRepository(JsonFileStore store)
{
    const string Source = nameof(OrderRepository);

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, "Beginning transaction...", 100, ct);
        await Latency.StepAsync(Source, $"Saving Order #{order.Id} ({order.Status})...", 400, ct);

        // Replace the order with the same Id, otherwise add it
        await store.UpdateAsync<Order>(DataFiles.Orders, orders =>
        {
            int index = orders.FindIndex(o => o.Id == order.Id);
            if (index >= 0)
                orders[index] = order;
            else
                orders.Add(order);
        }, ct);

        await Latency.StepAsync(Source, "Committing...", 200, ct);
        Log.Info($"{Source} -> Order #{order.Id} saved");
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, "Executing query for all orders...", Latency.Between(300, 600), ct);

        List<Order> orders = await store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        Log.Info($"{Source} -> {orders.Count} orders returned");
        return orders;
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, $"Executing query for orders with status {status}...", Latency.Between(300, 600), ct);

        List<Order> all = await store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        List<Order> filtered = all.Where(o => o.Status == status).ToList();
        Log.Info($"{Source} -> {filtered.Count} orders with status {status}");
        return filtered;
    }
}