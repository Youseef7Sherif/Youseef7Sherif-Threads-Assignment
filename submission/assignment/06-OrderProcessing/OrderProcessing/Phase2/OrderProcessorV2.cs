using System.Collections.Concurrent;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public sealed class OrderProcessorV2(
    ProductRepository products,
    OrderRepository orders,
    OrderPricer pricer,
    InvoiceGenerator invoices)
{
    // What each order has reserved so far, so exactly that can be released later
    readonly ConcurrentDictionary<int, List<OrderItem>> _reserved = new();

    // Does NOT catch exceptions: the caller decides what to do with a failed order
    public async Task<bool> ProcessValidatedOrderAsync(Order order, CancellationToken ct)
    {
        var reserved = new List<OrderItem>();
        _reserved[order.Id] = reserved;

        // 1) reserve stock item by item
        foreach (OrderItem item in order.Items)
        {
            bool ok = await products.ReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (!ok)
            {
                Product? product = await products.GetByIdAsync(item.ProductId, ct);
                await ReleaseReservedAsync(order, ct);

                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{product?.Name}'";
                await orders.SaveAsync(order, ct);

                _reserved.TryRemove(order.Id, out _);
                Log.Error($"OrderProcessorV2 -> Order #{order.Id} rejected: {order.FailureReason}");
                return false;
            }
            reserved.Add(item);
        }

        // 2) price, 3) invoice
        await pricer.PriceOrderAsync(order, ct);
        await invoices.GenerateAsync(order, ct);

        // 4) done
        order.Status = OrderStatus.Completed;
        order.FailureReason = null;
        await orders.SaveAsync(order, ct);

        _reserved.TryRemove(order.Id, out _);
        Log.Success($"OrderProcessorV2 -> Order #{order.Id} completed (total {order.TotalAmount:F2})");
        return true;
    }

    // Gives back only the stock this order really reserved
    public async Task ReleaseReservedAsync(Order order, CancellationToken ct)
    {
        if (!_reserved.TryGetValue(order.Id, out List<OrderItem>? reserved))
            return;

        while (reserved.Count > 0)
        {
            OrderItem item = reserved[^1];
            await products.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
            reserved.RemoveAt(reserved.Count - 1);
        }
    }
}