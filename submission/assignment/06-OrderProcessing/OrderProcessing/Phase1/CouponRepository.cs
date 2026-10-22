using OrderProcessing.Shared;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class CouponRepository(JsonFileStore store)
{
    const string Source = nameof(CouponRepository);

    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct)
    {
        await Latency.StepAsync(Source, "Opening connection...", 200, ct);
        await Latency.StepAsync(Source, $"Executing query for Coupon '{code}'...", Latency.Between(300, 600), ct);

        List<Coupon> coupons = await store.ReadAllAsync<Coupon>(DataFiles.Coupons, ct);
        Coupon? coupon = coupons.FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        Log.Info(coupon is null
            ? $"{Source} -> Coupon '{code}' not found"
            : $"{Source} -> Coupon '{code}' found ({coupon.DiscountPercent}%)");
        return coupon;
    }
}