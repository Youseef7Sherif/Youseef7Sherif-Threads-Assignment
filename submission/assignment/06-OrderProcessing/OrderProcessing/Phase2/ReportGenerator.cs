using System.Text.Json;
using OrderProcessing.Phase1;
using OrderProcessing.Shared;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class ReportGenerator(OrderRepository orders, ProductRepository products)
{
    public async Task<Report> GenerateAsync(CancellationToken ct)
    {
        List<Order> all = await orders.GetAllAsync(ct);
        List<Product> catalog = await products.GetAllAsync(ct);

        List<Order> completed = all.Where(o => o.Status == OrderStatus.Completed).ToList();
        decimal revenue = completed.Sum(o => o.TotalAmount);

        // highest total quantity over Completed orders, ties -> lowest ProductId
        var top = completed.SelectMany(o => o.Items)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ProductId)
            .FirstOrDefault();

        var report = new Report
        {
            TotalOrders = all.Count,
            TotalRevenue = Money.Round(revenue),
            CompletedCount = completed.Count,
            RejectedCount = all.Count(o => o.Status == OrderStatus.Rejected),
            FailedCount = all.Count(o => o.Status == OrderStatus.Failed),
            PaymentFailedCount = all.Count(o => o.Status == OrderStatus.PaymentFailed),
            TopSellingProduct = top is null ? "" : catalog.First(p => p.Id == top.ProductId).Name,
            TopSellingQuantity = top?.Quantity ?? 0,
            AverageOrderValue = completed.Count == 0 ? 0 : Money.Round(revenue / completed.Count),
            GeneratedAt = DateTime.Now
        };

        Directory.CreateDirectory(DataFiles.ReportsFolder);
        string path = Path.Combine(DataFiles.ReportsFolder, $"report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, JsonFileStore.Options), ct);

        return report;
    }
}