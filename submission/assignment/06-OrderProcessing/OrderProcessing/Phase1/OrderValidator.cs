using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public sealed class OrderValidator(
    CustomerRepository customers,
    ProductRepository products,
    CouponRepository coupons)
{
    // Sets Status (Validated / Rejected) and FailureReason on the order.
    // Returns true when the order passed. The caller saves the order.
    public async Task<bool> ValidateOrderAsync(Order order, CancellationToken ct)
    {
        string? failure = await FindFailureAsync(order, ct);

        if (failure is null)
        {
            order.Status = OrderStatus.Validated;
            order.FailureReason = null;
            Log.Success($"OrderValidator -> Order #{order.Id} validated");
            return true;
        }

        order.Status = OrderStatus.Rejected;
        order.FailureReason = failure;
        Log.Error($"OrderValidator -> Order #{order.Id} rejected: {failure}");
        return false;
    }

    // Returns the first failure text, or null if every check passes
    async Task<string?> FindFailureAsync(Order order, CancellationToken ct)
    {
        // 1) customer exists, 2) customer is active
        Customer? customer = await customers.GetByIdAsync(order.CustomerId, ct);
        if (customer is null)
            return $"Customer #{order.CustomerId} not found";
        if (!customer.IsActive)
            return "Customer is inactive";

        // 3) every product exists
        var found = new List<(OrderItem Item, Product Product)>();
        foreach (OrderItem item in order.Items)
        {
            Product? product = await products.GetByIdAsync(item.ProductId, ct);
            if (product is null)
                return $"Product #{item.ProductId} not found";
            found.Add((item, product));
        }

        // 4) enough stock (only checked here, not reserved)
        foreach (var (item, product) in found)
        {
            if (product.StockQuantity < item.Quantity)
                return $"Insufficient stock for '{product.Name}'";
        }

        // 5) coupon exists, 6) coupon not expired
        if (order.CouponCode is not null)
        {
            Coupon? coupon = await coupons.GetByCodeAsync(order.CouponCode, ct);
            if (coupon is null)
                return $"Coupon '{order.CouponCode}' does not exist";
            if (coupon.ExpiryDate < DateTime.Today)
                return $"Coupon '{order.CouponCode}' has expired";
        }

        return null;
    }
}