using OrderProcessing.Shared;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public static class PricingRules
{
    // loyalty (Bronze 0, Silver 5, Gold 10) + coupon %, capped at 30
    public static int DiscountPercent(LoyaltyTier tier, int couponPercent)
    {
        int loyalty = tier switch
        {
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };
        return Math.Min(loyalty + couponPercent, 30);
    }

    // 5.00 + 1.50 x quantity + 2.00 x weight(kg); free when SubTotal >= 1000
    public static decimal ShippingCost(Order order, List<Product> products)
    {
        if (order.SubTotal >= 1000m)
            return 0m;

        int quantity = order.Items.Sum(i => i.Quantity);
        decimal weight = order.Items.Sum(i => products.First(p => p.Id == i.ProductId).WeightKg * i.Quantity);
        return Money.Round(5.00m + 1.50m * quantity + 2.00m * weight);
    }
}