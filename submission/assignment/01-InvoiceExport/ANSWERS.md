# Part 01 — Invoice Export

**Student name:** <Youssef Mohamed Sherif>
**Group / ID:** <Group 2>

## 1. Original behaviour (R1)

I ran the starter project twice before changing anything. Both runs took the same time, and the invoices were written one after another in order.

```
Starting invoice export...
Customer files: ...\InvoiceExport-starter\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: ...\InvoiceExport-starter\invoices

Wrote ...\invoices\invoice-1001.txt
Wrote ...\invoices\invoice-1002.txt
Wrote ...\invoices\invoice-1003.txt
Wrote ...\invoices\invoice-1004.txt
Wrote ...\invoices\invoice-1005.txt
Export finished in 6.0 s
```

Run 1: **6.0 s**, Run 2: **6.0 s**.

The time matches the delays in the code:
`800 ms (load pending orders) + 5 × (400 + 300 + 300) ms = 5800 ms ≈ 6.0 s`.

## 2. Problems with this design (R2)

1. **Threads are blocked while waiting.** `Thread.Sleep`, `File.ReadAllText` and `File.WriteAllText` all block the calling thread. The thread does nothing useful while it waits for the database or the disk. With many orders, a server would run out of threads and become slow.
2. **Everything runs one after another.** Order 1002 does not start until order 1001 has finished all three steps, even though the orders do not depend on each other. The total time grows linearly: 100 orders would take about 100 seconds.
3. **No way to cancel.** Once the export starts, there is no `CancellationToken`, so it cannot be stopped halfway.
4. **The database is queried twice per order.** `GetPendingOrders()` already returns all the orders, and then `GetOrderById()` loads each one again (400 ms each). I kept it because the assignment says the business flow must stay the same, but it adds 2 seconds to the total.

## 3. What I changed and why (R3, R4)

I kept the same flow for every order (load order → read customer → write invoice) and the same invoice content. I changed these things:

| File                      | Change                                                                                                                                                                                |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OrderDatabase.cs`        | `GetPendingOrders` and `GetOrderById` became `GetPendingOrdersAsync` and `GetOrderByIdAsync`. They return `Task<...>` and use `await Task.Delay(...)` instead of `Thread.Sleep(...)`. |
| `CustomerFileStore.cs`    | `ReadProfile` became `ReadProfileAsync`, using `Task.Delay` and `File.ReadAllTextAsync`.                                                                                              |
| `InvoiceFileWriter.cs`    | `WriteInvoice` became `WriteInvoiceAsync`, using `Task.Delay` and `File.WriteAllTextAsync`.                                                                                           |
| `InvoiceExportService.cs` | `ExportAllPending` became `ExportAllPendingAsync`. The work of one order moved to a private method `ExportOneAsync`.                                                                  |
| `Program.cs`              | The last line is now `await export.ExportAllPendingAsync();`.                                                                                                                         |

**The main change** is in `ExportAllPendingAsync`. First I start the export of every order without awaiting it and keep the tasks in a list. Then I await them one by one in a second loop:

```csharp
var tasks = new List<Task<string>>();
foreach (Order order in orders)
    tasks.Add(ExportOneAsync(order));

foreach (Task<string> task in tasks)
{
    string path = await task;
    Console.WriteLine($"Wrote {path}");
}
```

Why this works:

- Using `await` instead of `Thread.Sleep` frees the thread while it waits. The thread can go back to the thread pool instead of standing still.
- Starting all the tasks before awaiting any of them makes the five orders wait at the same time. Each `ExportOneAsync` call returns an unfinished `Task` at its first `await`, so the first loop finishes almost immediately.
- The second loop awaits in list order, so the output order stays 1001 → 1005.
- I did not use `Task.WhenAll`, as the assignment rules require.
- Inside one order, each step still waits for the previous one, because writing the invoice needs the order and the customer.

## 4. Output after the change

```
Starting invoice export...
Customer files: ...\InvoiceExport-starter\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: ...\InvoiceExport-starter\invoices

Wrote ...\invoices\invoice-1001.txt
Wrote ...\invoices\invoice-1002.txt
Wrote ...\invoices\invoice-1003.txt
Wrote ...\invoices\invoice-1004.txt
Wrote ...\invoices\invoice-1005.txt
Export finished in 1.8 s
```

Run 1: **1.9 s**, Run 2: **1.8 s**.

## 5. Before vs after

|             | Before                             | After                                 |
| ----------- | ---------------------------------- | ------------------------------------- |
| Orders      | one after another                  | all five waiting at the same time     |
| Waiting     | blocks the thread (`Thread.Sleep`) | frees the thread (`await Task.Delay`) |
| Time        | 6.0 s                              | 1.8 – 1.9 s                           |
| Calculation | 800 + 5 × 1000 = 5800 ms           | 800 + 1000 = 1800 ms                  |
| Output      | same 5 invoices, order 1001 → 1005 | same 5 invoices, order 1001 → 1005    |

The new time is about 800 ms (loading the pending orders) plus the time of one order (1000 ms), because the five orders now wait together.
