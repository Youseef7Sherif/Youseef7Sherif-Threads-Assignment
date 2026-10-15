using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public async Task ExportAllPendingAsync()
    {
        var watch = Stopwatch.StartNew();
        List<Order> orders = await _db.GetPendingOrdersAsync();

        // 1) Start every export (no await yet) so they all run at the same time
        var tasks = new List<Task<string>>();
        foreach (Order order in orders)
            tasks.Add(ExportOneAsync(order));

        // 2) Await them one by one
        foreach (Task<string> task in tasks)
        {
            string path = await task;
            Console.WriteLine($"Wrote {path}");
        }

        Console.WriteLine($"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }

    // Same flow as before: load order -> read customer -> write invoice
    async Task<string> ExportOneAsync(Order order)
    {
        Order fresh = await _db.GetOrderByIdAsync(order.Id)
            ?? throw new InvalidOperationException($"Order {order.Id} missing");

        CustomerProfile customer = await _customers.ReadProfileAsync(fresh.CustomerId);
        return await _invoices.WriteInvoiceAsync(fresh, customer);
    }
}