namespace FitnessMerchant.Api.Infrastructure.Payments;

/// <summary>
/// Card details for one charge. Never stored or logged: <see cref="ToString"/> is masked so the object cannot
/// leak a card number into logs even by accident.
/// </summary>
public sealed record CardDetails(string Number, int ExpiryMonth, int ExpiryYear, string Cvc, string NameOnCard)
{
    public string Last4 => Number[^4..];

    public override string ToString() => $"CardDetails {{ Number = **** {Last4}, Expiry = **/**, Cvc = *** }}";
}

public sealed record PaymentRequest(Guid OrderId, string OrderNumber, decimal Amount, string Currency, CardDetails Card);

public abstract record PaymentResult
{
    public sealed record Succeeded(string Reference, string CardBrand, string CardLast4) : PaymentResult;

    public sealed record Declined(string Reason) : PaymentResult;
}

/// <summary>D010: selected by <c>Payments__Provider</c>. M07 adds Stripe Checkout.</summary>
public interface IPaymentGateway
{
    string Name { get; }

    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken);
}

public sealed class PaymentOptions
{
    public const string Section = "Payments";
    public const string Fake = "Fake";

    public string Provider { get; init; } = Fake;
}

/// <summary>
/// Deterministic test gateway for local runs, tests and E2E (no network): 4242 4242 4242 4242 succeeds,
/// 4000 0000 0000 0002 is declined, any other number is declined with a hint. Never charges anything.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string SuccessCard = "4242424242424242";
    public const string DeclineCard = "4000000000000002";

    public string Name => PaymentOptions.Fake;

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        PaymentResult result = request.Card.Number switch
        {
            SuccessCard => new PaymentResult.Succeeded($"fake_{Guid.CreateVersion7():N}", Brand(request.Card.Number), request.Card.Last4),
            DeclineCard => new PaymentResult.Declined("Your card was declined."),
            _ => new PaymentResult.Declined("This demo only accepts the test cards shown at checkout."),
        };
        return Task.FromResult(result);
    }

    internal static string Brand(string number) => number[0] switch
    {
        '4' => "Visa",
        '5' => "Mastercard",
        '3' => "Amex",
        _ => "Card",
    };
}

public static class PaymentSetup
{
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(PaymentOptions.Section).Get<PaymentOptions>() ?? new PaymentOptions();
        if (!string.Equals(options.Provider, PaymentOptions.Fake, StringComparison.Ordinal))
        {
            // Fail fast rather than silently taking orders without a working gateway.
            throw new InvalidOperationException(
                $"Unknown payment provider '{options.Provider}' (Payments__Provider). Supported: {PaymentOptions.Fake}.");
        }

        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        return services;
    }
}
