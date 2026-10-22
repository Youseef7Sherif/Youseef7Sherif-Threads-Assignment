using OrderProcessing.Phase1;
using OrderProcessing.Shared;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class PricingService(
    JsonFileStore store,
    CustomerRepository customers,
    CouponRepository coupons,
    IEnumerable<int> failShippingForOrderIds)
{
    const string Source = nameof(PricingService);
    readonly HashSet<int> _failShipping = failShippingForOrderIds.ToHashSet();

    // DB query: fills UnitPrice on every item and returns the SubTotal
    public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct)
    {
        await Latency.StepAsync(Source, $"Order #{order.Id} subtotal: opening connection...", 200, ct);
        await Latency.StepAsync(Source, $"Order #{order.Id} subtotal: executing query...", Latency.Between(300, 600), ct);

        List<Product> products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);

        decimal subTotal = 0;
        foreach (OrderItem item in order.Items)
        {
            item.UnitPrice = products.First(p => p.Id == item.ProductId).Price;
            subTotal += item.UnitPrice * item.Quantity;
        }
        return Money.Round(subTotal);
    }

    // Discount API: needs order.SubTotal to be already filled
    public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct)
    {
        await Latency.StepAsync(Source, $"Order #{order.Id} discount API: DNS/handshake...", 150, ct);
        await Latency.StepAsync(Source, $"Order #{order.Id} discount API: sending request...", 300, ct);
        await Latency.StepAsync(Source, $"Order #{order.Id} discount API: waiting for response...", Latency.Between(500, 1500), ct);

        Customer customer = await customers.GetByIdAsync(order.CustomerId, ct)
            ?? throw new CustomerNotFoundException($"Customer #{order.CustomerId} not found");

        int couponPercent = 0;
        if (order.CouponCode is not null)
        {
            Coupon coupon = await coupons.GetByCodeAsync(order.CouponCode, ct)
                ?? throw new InvalidCouponException($"Coupon '{order.CouponCode}' does not exist");
            if (coupon.ExpiryDate < DateTime.Today)
                throw new InvalidCouponException($"Coupon '{order.CouponCode}' has expired");
            couponPercent = coupon.DiscountPercent;
        }

        int percent = PricingRules.DiscountPercent(customer.LoyaltyTier, couponPercent);
        return Money.Round(order.SubTotal * percent / 100m);
    }

    // Shipping API: throws right after the handshake for the configured order ids
    public async Task<decimal> CalculateShippingCostAsync(Order order, CancellationToken ct)
    {
        await Latency.StepAsync(Source, $"Order #{order.Id} shipping API: DNS/handshake...", 150, ct);

        if (_failShipping.Contains(order.Id))
            throw new ShippingProviderUnavailableException("Shipping provider unavailable");

        await Latency.StepAsync(Source, $"Order #{order.Id} shipping API: sending request...", 300, ct);
        await Latency.StepAsync(Source, $"Order #{order.Id} shipping API: waiting for response...", Latency.Between(500, 1500), ct);

        List<Product> products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);
        return PricingRules.ShippingCost(order, products);
    }
}