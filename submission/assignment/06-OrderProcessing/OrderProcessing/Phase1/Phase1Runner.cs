using System.Diagnostics;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public static class Phase1Runner
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var store = new JsonFileStore();
        var incomingRepo = new IncomingOrderRepository(store);
        var orderRepo = new OrderRepository(store);

        var syncValidator = new SyncOrderValidator(
            new SyncCustomerRepository(store),
            new SyncProductRepository(store),
            new SyncCouponRepository(store));

        var validator = new OrderValidator(
            new CustomerRepository(store),
            new ProductRepository(store),
            new CouponRepository(store));

        Console.WriteLine("===== Phase 1 - Async Data Access Layer =====");

        // ---------- 1) sync run (results are NOT saved) ----------
        await DataSeeder.ResetAsync(ct);
        List<Order> syncOrders = await incomingRepo.GetAllAsync(ct);

        Console.WriteLine();
        Console.WriteLine("--- Sync run (Thread.Sleep, one by one) ---");
        var watch = Stopwatch.StartNew();
        foreach (Order order in syncOrders)
        {
            ct.ThrowIfCancellationRequested();
            syncValidator.ValidateOrder(order);
        }
        watch.Stop();
        double syncSeconds = watch.Elapsed.TotalSeconds;

        // ---------- 2) async run (every order is saved) ----------
        await DataSeeder.ResetAsync(ct);
        List<Order> orders = await incomingRepo.GetAllAsync(ct);

        Console.WriteLine();
        Console.WriteLine("--- Async run (Task.Delay, one by one) ---");
        int finished = 0;
        int cancelled = 0;

        var validationWatch = new Stopwatch();   // validation time only
        watch.Restart();
        foreach (Order order in orders)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                validationWatch.Start();
                await validator.ValidateOrderAsync(order, ct);
                validationWatch.Stop();

                await orderRepo.SaveAsync(order, ct);
                finished++;
            }
            catch (OperationCanceledException)
            {
                order.Status = OrderStatus.Pending;
                order.FailureReason = "Cancelled";
                await orderRepo.SaveAsync(order, CancellationToken.None);
                cancelled++;
                break;
            }
        }
        watch.Stop();
        double asyncTotalSeconds = watch.Elapsed.TotalSeconds;
        double asyncSeconds = validationWatch.Elapsed.TotalSeconds;

        if (ct.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.WriteLine($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");
            return;
        }

        // ---------- 3) summary, read back from orders.json ----------
        List<Order> all = await orderRepo.GetAllAsync(ct);
        List<Order> validated = await orderRepo.GetByStatusAsync(OrderStatus.Validated, ct);
        List<Order> rejected = await orderRepo.GetByStatusAsync(OrderStatus.Rejected, ct);

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {all.Count}");
        Console.WriteLine($"Validated: {validated.Count}");
        Console.WriteLine($"Rejected: {rejected.Count}");
        foreach (Order order in rejected.OrderBy(o => o.Id))
            Console.WriteLine($"- Order #{order.Id}: {order.FailureReason}");
        Console.WriteLine($"Sync Time: {syncSeconds:F1} seconds");
        Console.WriteLine($"Async Time: {asyncSeconds:F1} seconds");
        Console.WriteLine($"Sync Time: {syncSeconds:F1} seconds");
        Console.WriteLine($"Async Time: {asyncSeconds:F1} seconds");
        Console.WriteLine($"Async Time including saving: {asyncTotalSeconds:F1} seconds");
        Console.WriteLine($"orders.json now contains {all.Count} records.");
        Console.WriteLine($"orders.json now contains {all.Count} records.");
    }
}