using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public sealed class SyncOrderValidator(
    SyncCustomerRepository customers,
    SyncProductRepository products,
    SyncCouponRepository coupons)
{
    public bool ValidateOrder(Order order)
    {
        string? failure = FindFailure(order);

        if (failure is null)
        {
            order.Status = OrderStatus.Validated;
            order.FailureReason = null;
            Log.Success($"SyncOrderValidator -> Order #{order.Id} validated");
            return true;
        }

        order.Status = OrderStatus.Rejected;
        order.FailureReason = failure;
        Log.Error($"SyncOrderValidator -> Order #{order.Id} rejected: {failure}");
        return false;
    }

    string? FindFailure(Order order)
    {
        Customer? customer = customers.GetById(order.CustomerId);
        if (customer is null)
            return $"Customer #{order.CustomerId} not found";
        if (!customer.IsActive)
            return "Customer is inactive";

        var found = new List<(OrderItem Item, Product Product)>();
        foreach (OrderItem item in order.Items)
        {
            Product? product = products.GetById(item.ProductId);
            if (product is null)
                return $"Product #{item.ProductId} not found";
            found.Add((item, product));
        }

        foreach (var (item, product) in found)
        {
            if (product.StockQuantity < item.Quantity)
                return $"Insufficient stock for '{product.Name}'";
        }

        if (order.CouponCode is not null)
        {
            Coupon? coupon = coupons.GetByCode(order.CouponCode);
            if (coupon is null)
                return $"Coupon '{order.CouponCode}' does not exist";
            if (coupon.ExpiryDate < DateTime.Today)
                return $"Coupon '{order.CouponCode}' has expired";
        }

        return null;
    }
}