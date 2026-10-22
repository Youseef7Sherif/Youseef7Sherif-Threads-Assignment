using OrderProcessing.Shared;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class CustomerRepository(JsonFileStore store)
{
    const string Source = nameof(CustomerRepository);

    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, $"Executing query for Customer #{id}...", Latency.Between(300, 600), ct);

        List<Customer> customers = await store.ReadAllAsync<Customer>(DataFiles.Customers, ct);
        Customer? customer = customers.FirstOrDefault(c => c.Id == id);

        Log.Info(customer is null
            ? $"{Source} -> Customer #{id} not found"
            : $"{Source} -> Customer #{id} found ({customer.Name})");
        return customer;
    }
}