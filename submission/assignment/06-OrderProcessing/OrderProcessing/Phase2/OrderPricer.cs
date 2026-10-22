using OrderProcessing.Shared;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public sealed class OrderPricer(PricingService pricing)
{
    // SubTotal first, then discount and shipping run AT THE SAME TIME
    public async Task PriceOrderAsync(Order order, CancellationToken ct)
    {
        order.SubTotal = await pricing.CalculateSubTotalAsync(order, ct);

        Task<decimal> discountTask = pricing.GetDiscountAsync(order, ct);
        Task<decimal> shippingTask = pricing.CalculateShippingCostAsync(order, ct);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;
        order.TotalAmount = Money.Round(order.SubTotal - order.Discount + order.ShippingCost);
    }

    // Same work, but each call is awaited right after it starts
    public async Task PriceOrderSequentialAsync(Order order, CancellationToken ct)
    {
        order.SubTotal = await pricing.CalculateSubTotalAsync(order, ct);
        order.Discount = await pricing.GetDiscountAsync(order, ct);
        order.ShippingCost = await pricing.CalculateShippingCostAsync(order, ct);
        order.TotalAmount = Money.Round(order.SubTotal - order.Discount + order.ShippingCost);
    }
}