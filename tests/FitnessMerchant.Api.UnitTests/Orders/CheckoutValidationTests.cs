using FitnessMerchant.Api.Features.Orders;
using FitnessMerchant.Api.Infrastructure.Payments;

namespace FitnessMerchant.Api.UnitTests.Orders;

public class CheckoutValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly ShippingInput ValidShipping =
        new("Ana Petrova", "10 Example Street", null, "Skopje", "1000", "North Macedonia", "+389 70 000 000", "ana@test.example");

    private static readonly PaymentInput ValidPayment = new("4242 4242 4242 4242", "12/30", "123", "Ana Petrova");

    [Fact]
    public void A_valid_request_produces_address_email_and_normalized_card()
    {
        var (address, email, card, errors) = CheckoutValidation.Validate(new CheckoutRequest(ValidShipping, ValidPayment, null), Now);

        Assert.Empty(errors);
        Assert.Equal("Skopje", address!.City);
        Assert.Null(address.Line2);
        Assert.Equal("ana@test.example", email);
        Assert.Equal(("4242424242424242", 12, 2030, "4242"), (card!.Number, card.ExpiryMonth, card.ExpiryYear, card.Last4));
    }

    [Theory]
    [InlineData("4242 4242 4242 4241", "payment.cardNumber")] // fails Luhn
    [InlineData("4242-4242-4242-424A", "payment.cardNumber")] // not digits
    [InlineData("4242", "payment.cardNumber")]
    public void Card_numbers_are_checked(string number, string field) =>
        AssertError(new CheckoutRequest(ValidShipping, ValidPayment with { CardNumber = number }, null), field);

    [Theory]
    [InlineData("09/26")] // ended before Now (October 2026)
    [InlineData("13/30")]
    [InlineData("1230")]
    [InlineData("")]
    public void Expiry_must_be_a_future_MM_YY(string expiry) =>
        AssertError(new CheckoutRequest(ValidShipping, ValidPayment with { Expiry = expiry }, null), "payment.expiry");

    [Fact]
    public void The_current_month_is_still_valid() =>
        Assert.Empty(CheckoutValidation.Validate(new CheckoutRequest(ValidShipping, ValidPayment with { Expiry = "10/26" }, null), Now).Errors);

    [Theory]
    [InlineData("12")]
    [InlineData("12a")]
    [InlineData("12345")]
    public void Cvc_is_3_or_4_digits(string cvc) =>
        AssertError(new CheckoutRequest(ValidShipping, ValidPayment with { Cvc = cvc }, null), "payment.cvc");

    [Fact]
    public void Missing_or_invalid_shipping_fields_are_reported_with_form_keys()
    {
        var shipping = ValidShipping with { FullName = " ", City = null, Country = "Atlantis", Email = "not-an-email", Phone = "abc" };

        var errors = CheckoutValidation.Validate(new CheckoutRequest(shipping, ValidPayment, null), Now).Errors;

        Assert.Equal(
            ["shipping.city", "shipping.country", "shipping.email", "shipping.fullName", "shipping.phone"],
            errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Card_input_never_prints_its_number()
    {
        var card = new CardDetails("4242424242424242", 12, 2030, "123", "Ana");

        Assert.DoesNotContain("424242424242", card.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("123", card.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("4242", ValidPayment.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FakePaymentGateway.SuccessCard, true)]
    [InlineData(FakePaymentGateway.DeclineCard, false)]
    [InlineData("5555555555554444", false)]
    public async Task Fake_gateway_is_deterministic(string number, bool succeeds)
    {
        var result = await new FakePaymentGateway().ChargeAsync(
            new PaymentRequest(Guid.NewGuid(), "FM-1", 10m, "EUR", new CardDetails(number, 12, 2030, "123", "A")), CancellationToken.None);

        Assert.Equal(succeeds, result is PaymentResult.Succeeded);
    }

    private static void AssertError(CheckoutRequest request, string field) =>
        Assert.Contains(field, CheckoutValidation.Validate(request, Now).Errors.Keys);
}
