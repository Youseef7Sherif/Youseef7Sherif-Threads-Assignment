using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Diagnostics;

namespace OrderProcessing.Phase2;

public static class Phase2Runner
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var store = new JsonFileStore();
        var customerRepo = new CustomerRepository(store);
        var productRepo = new ProductRepository(store);
        var couponRepo = new CouponRepository(store);
        var orderRepo = new OrderRepository(store);
        var incomingRepo = new IncomingOrderRepository(store);

        var validator = new OrderValidator(customerRepo, productRepo, couponRepo);
        var pricing = new PricingService(store, customerRepo, couponRepo, failShippingForOrderIds: [7]);
        var pricer = new OrderPricer(pricing);
        var processor = new OrderProcessorV2(productRepo, orderRepo, pricer, new InvoiceGenerator());
        var raceDemo = new RaceConditionDemo(store, productRepo);
        var reports = new ReportGenerator(orderRepo, productRepo);

        int finished = 0;
        int cancelled = 0;

        try
        {
            Console.WriteLine("===== Phase 2 - Concurrent Processing =====");

            // ---------- 1) race demo, then pricing comparison on order #1 ----------
            await DataSeeder.ResetAsync(ct);
            await raceDemo.RunAsync(ct);
            await DataSeeder.ResetAsync(ct);

            List<Order> incoming = await incomingRepo.GetAllAsync(ct);
            Order order1 = incoming.First(o => o.Id == 1);

            Console.WriteLine();
            Console.WriteLine("--- Pricing order #1 (not saved) ---");
            var watch = Stopwatch.StartNew();
            await pricer.PriceOrderSequentialAsync(order1, ct);
            double sequentialSeconds = watch.Elapsed.TotalSeconds;
            PrintPrice("Sequential", order1);

            watch.Restart();
            await pricer.PriceOrderAsync(order1, ct);
            double concurrentSeconds = watch.Elapsed.TotalSeconds;
            PrintPrice("Concurrent", order1);
            Console.WriteLine($"Pricing time: sequential {sequentialSeconds:F1} s, concurrent {concurrentSeconds:F1} s");

            // ---------- 2) validate all 20 orders concurrently ----------
            List<Order> orders = await incomingRepo.GetAllAsync(ct);

            Console.WriteLine();
            Console.WriteLine("--- Validating all orders concurrently ---");
            Log.ResetThreadTracking();
            var total = Stopwatch.StartNew();

            var validation = new List<(Order Order, Task Task)>();
            foreach (Order order in orders)
            {
                if (ct.IsCancellationRequested)
                    break;
                validation.Add((order, ValidateAndSaveAsync(order, validator, orderRepo, ct)));
            }

            foreach ((Order order, Task task) in validation)
            {
                try
                {
                    await task;
                    if (order.Status == OrderStatus.Rejected)
                        finished++;
                }
                catch (OperationCanceledException)
                {
                    await MarkCancelledAsync(order, orderRepo);
                    cancelled++;
                }
            }

            // ---------- 3) process every Validated order concurrently ----------
            List<Order> validated = orders.Where(o => o.Status == OrderStatus.Validated).ToList();

            Console.WriteLine();
            Console.WriteLine($"--- Processing {validated.Count} validated orders concurrently ---");

            var started = new List<Order>();
            var tasks = new List<Task<bool>>();
            foreach (Order order in validated)
            {
                if (ct.IsCancellationRequested)
                {
                    await MarkCancelledAsync(order, orderRepo);
                    cancelled++;
                    continue;
                }
                started.Add(order);
                tasks.Add(processor.ProcessValidatedOrderAsync(order, ct));
            }

            if (tasks.Count > 0)
                Console.WriteLine($"Status of task[0] right after creation: {tasks[0].Status}");

            try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { }

            Console.WriteLine("Status of every task after 1 second:");
            for (int i = 0; i < tasks.Count; i++)
                Console.WriteLine($"  Order #{started[i].Id}: {tasks[i].Status}");

            // A failing order must not stop the others
            for (int i = 0; i < tasks.Count; i++)
            {
                Order order = started[i];
                try
                {
                    await tasks[i];
                    finished++;
                }
                catch (OperationCanceledException)
                {
                    await processor.ReleaseReservedAsync(order, CancellationToken.None);
                    await MarkCancelledAsync(order, orderRepo);
                    cancelled++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Order #{order.Id} task is {tasks[i].Status}: {ex.Message}");

                    // clean-up must finish, so it does not use the (possibly cancelled) token
                    await processor.ReleaseReservedAsync(order, CancellationToken.None);
                    order.Status = OrderStatus.Failed;
                    order.FailureReason = ex.Message;
                    await orderRepo.SaveAsync(order, CancellationToken.None);
                    finished++;
                }
            }
            total.Stop();

            Console.WriteLine();
            Console.WriteLine("Final status of every task:");
            for (int i = 0; i < tasks.Count; i++)
                Console.WriteLine($"  Order #{started[i].Id}: task {tasks[i].Status}, order {started[i].Status}");
            Console.WriteLine($"Total time (validate + process, concurrent): {total.Elapsed.TotalSeconds:F1} seconds");
            Console.WriteLine($"Distinct threads used: {Log.DistinctThreads}, ThreadPool.ThreadCount: {ThreadPool.ThreadCount}");
            // ---------- 4) check orders.json ----------
            List<Order> saved = await orderRepo.GetAllAsync(ct);
            Console.WriteLine();
            Console.WriteLine($"orders.json contains {saved.Count} records (expected 20).");
            foreach (var group in saved.GroupBy(o => o.Status).OrderBy(g => g.Key))
                Console.WriteLine($"  {group.Key}: {group.Count()}");

            // ---------- 5) report ----------
            Console.WriteLine();
            Task<Report> reportTask = reports.GenerateAsync(ct);
            Console.WriteLine("Generating report...");
            Report report = await reportTask;
            PrintReport(report);

            // ---------- 6) the one allowed .Result demo ----------
            Console.WriteLine();
            BlockingDemo.Run();
        }
        catch (OperationCanceledException)
        {
            // handled below
        }

        if (ct.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.WriteLine($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");
        }
    }

    static async Task ValidateAndSaveAsync(
        Order order, OrderValidator validator, OrderRepository repo, CancellationToken ct)
    {
        await validator.ValidateOrderAsync(order, ct);
        await repo.SaveAsync(order, ct);
    }

    // A cancelled order is never Failed: it goes back to Pending with the reason "Cancelled"
    static async Task MarkCancelledAsync(Order order, OrderRepository repo)
    {
        order.Status = OrderStatus.Pending;
        order.FailureReason = "Cancelled";
        await repo.SaveAsync(order, CancellationToken.None);
    }

    static void PrintPrice(string label, Order o) =>
        Console.WriteLine($"{label}: SubTotal {o.SubTotal:F2} / Discount {o.Discount:F2} / Shipping {o.ShippingCost:F2} / Total {o.TotalAmount:F2}");

    static void PrintReport(Report r)
    {
        Console.WriteLine("===== Report =====");
        Console.WriteLine($"Total orders: {r.TotalOrders}");
        Console.WriteLine($"Completed: {r.CompletedCount}, Rejected: {r.RejectedCount}, Failed: {r.FailedCount}, PaymentFailed: {r.PaymentFailedCount}");
        Console.WriteLine($"Total revenue: {r.TotalRevenue:F2}");
        Console.WriteLine($"Average order value: {r.AverageOrderValue:F2}");
        Console.WriteLine($"Top selling product: {r.TopSellingProduct} ({r.TopSellingQuantity} units)");
        Console.WriteLine($"Generated at: {r.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
    }
}