using System.Text;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class InvoiceGenerator
{
    const string Source = nameof(InvoiceGenerator);

    public async Task GenerateAsync(Order order, CancellationToken ct)
    {
        Log.Info($"{Source} -> Rendering invoice PDF for Order #{order.Id}...");

        // CPU-bound work: Task.Run moves it to a thread-pool thread
        long checksum = await Task.Run(() => RenderInvoice(order.Id, ct), ct);

        Directory.CreateDirectory(DataFiles.InvoicesFolder);
        string path = Path.Combine(DataFiles.InvoicesFolder, $"invoice_{order.Id}.txt");
        await File.WriteAllTextAsync(path, BuildText(order, checksum), ct);

        Log.Success($"{Source} -> invoice_{order.Id}.txt written");
    }

    // Simulates rendering: 20,000,000 loop iterations (pure CPU, no waiting)
    static long RenderInvoice(int orderId, CancellationToken ct)
    {
        long h = orderId;
        for (int i = 0; i < 20_000_000; i++)
        {
            h = (h * 31 + i) % 1_000_000_007;
            if ((i & 0xFFFFF) == 0)
                ct.ThrowIfCancellationRequested();
        }
        return h;
    }

    static string BuildText(Order order, long checksum)
    {
        var text = new StringBuilder();
        text.AppendLine("SwiftBite Invoice");
        text.AppendLine("-----------------");
        text.AppendLine($"Order:     {order.Id}");
        text.AppendLine($"Customer:  {order.CustomerId}");
        foreach (OrderItem item in order.Items)
            text.AppendLine($"  Product #{item.ProductId} x{item.Quantity} @ {item.UnitPrice:F2}");
        text.AppendLine($"SubTotal:  {order.SubTotal:F2}");
        text.AppendLine($"Discount:  {order.Discount:F2}");
        text.AppendLine($"Shipping:  {order.ShippingCost:F2}");
        text.AppendLine($"Total:     {order.TotalAmount:F2}");
        text.AppendLine($"Render checksum: {checksum}");
        return text.ToString();
    }
}