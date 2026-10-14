using FitnessMerchant.Api.Domain.Orders;

namespace FitnessMerchant.Api.UnitTests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<OrderStatus, OrderStatus, bool> Transitions()
    {
        var allowed = new HashSet<(OrderStatus, OrderStatus)>
        {
            (OrderStatus.PendingPayment, OrderStatus.Paid),
            (OrderStatus.PendingPayment, OrderStatus.Cancelled),
            (OrderStatus.Paid, OrderStatus.Shipped),
            (OrderStatus.Paid, OrderStatus.Cancelled),
            (OrderStatus.Shipped, OrderStatus.Delivered),
        };
        var data = new TheoryData<OrderStatus, OrderStatus, bool>();
        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in Enum.GetValues<OrderStatus>())
            {
                data.Add(from, to, allowed.Contains((from, to)));
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void Only_the_SPEC_F10_transitions_are_allowed(OrderStatus from, OrderStatus to, bool allowed) =>
        Assert.Equal(allowed, OrderStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(OrderStatus.PendingPayment, true)]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Customers_can_cancel_until_the_order_ships(OrderStatus status, bool expected) =>
        Assert.Equal(expected, OrderStateMachine.CustomerCanCancel(status));

    [Fact]
    public void A_placed_order_waits_for_payment_and_records_every_change_with_timestamps()
    {
        var order = NewOrder();

        order.MarkPaid("ref", "Visa", "4242", Now.AddMinutes(1));
        order.TransitionTo(OrderStatus.Shipped, Now.AddDays(1), changedBy: null, note: null);
        order.TransitionTo(OrderStatus.Delivered, Now.AddDays(3), changedBy: null, note: "Left at the door.");

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal((Now.AddMinutes(1), Now.AddDays(1), Now.AddDays(3)), (order.PaidAt!.Value, order.ShippedAt!.Value, order.DeliveredAt!.Value));
        Assert.Equal(
            [(null, OrderStatus.PendingPayment), (OrderStatus.PendingPayment, OrderStatus.Paid), (OrderStatus.Paid, OrderStatus.Shipped), (OrderStatus.Shipped, OrderStatus.Delivered)],
            order.StatusChanges.Select(c => ((OrderStatus?)c.FromStatus, c.ToStatus)));
        Assert.Equal("Left at the door.", order.StatusChanges.Last().Note);
        Assert.Equal(("ref", "Visa", "4242"), (order.PaymentReference, order.CardBrand, order.CardLast4));
    }

    [Fact]
    public void An_invalid_transition_throws_and_changes_nothing()
    {
        var order = NewOrder();

        var error = Assert.Throws<InvalidOrderTransitionException>(() => order.TransitionTo(OrderStatus.Shipped, Now, null, null));

        Assert.Equal((OrderStatus.PendingPayment, OrderStatus.Shipped), (error.From, error.To));
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Single(order.StatusChanges);
    }

    [Fact]
    public void Cancelling_records_the_reason_and_is_final()
    {
        var order = NewOrder();

        order.Cancel("customer", Now, Guid.NewGuid());

        Assert.Equal((OrderStatus.Cancelled, "customer", Now), (order.Status, order.CancellationReason, order.CancelledAt!.Value));
        Assert.Throws<InvalidOrderTransitionException>(() => order.MarkPaid("x", "Visa", "4242", Now));
    }

    [Fact]
    public void Total_is_subtotal_plus_shipping()
    {
        var order = NewOrder();

        Assert.Equal(59.99m + 4.90m, order.Total);
    }

    private static Order NewOrder() => Order.Place(
        Guid.NewGuid(), "FM-000001", Guid.NewGuid(), [], 59.99m, 4.90m,
        new ShippingAddress("A B", "1 Street", null, "City", "1000", "Germany", "+49 000 000"),
        "a@test.example", "Fake", "key-12345678", Now);
}
