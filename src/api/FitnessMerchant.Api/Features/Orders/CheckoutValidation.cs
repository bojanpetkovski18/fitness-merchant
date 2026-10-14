using System.Net.Mail;
using System.Text.RegularExpressions;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Infrastructure.Payments;

namespace FitnessMerchant.Api.Features.Orders;

/// <summary>
/// Server-side validation of the checkout form (authoritative; the client checks the same basics for
/// convenience). Error keys match the form fields: "shipping.city", "payment.cardNumber".
/// </summary>
public static partial class CheckoutValidation
{
    public static readonly IReadOnlyList<string> Countries =
    [
        "Austria", "Belgium", "Croatia", "Denmark", "Finland", "France", "Germany", "Greece", "Ireland", "Italy",
        "Netherlands", "North Macedonia", "Poland", "Portugal", "Slovenia", "Spain", "Sweden",
    ];

    public static (ShippingAddress? Address, string? Email, CardDetails? Card, Dictionary<string, string[]> Errors) Validate(
        CheckoutRequest request, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Fail(string field, string message) => errors.TryAdd(field, [message]);
        string Text(string? value) => (value ?? "").Trim();

        var s = request.Shipping ?? new ShippingInput(null, null, null, null, null, null, null, null);
        var fullName = Text(s.FullName);
        var line1 = Text(s.Line1);
        var line2 = Text(s.Line2);
        var city = Text(s.City);
        var postalCode = Text(s.PostalCode);
        var country = Text(s.Country);
        var phone = Text(s.Phone);
        var email = Text(s.Email);

        Required("shipping.fullName", "Full name", fullName, 2, 100);
        Required("shipping.line1", "Address", line1, 3, 200);
        if (line2.Length > 200)
        {
            Fail("shipping.line2", "Address line 2 must be at most 200 characters.");
        }
        Required("shipping.city", "City", city, 2, 100);
        Required("shipping.postalCode", "Postal code", postalCode, 3, 20);
        if (postalCode.Length > 0 && !PostalCodePattern().IsMatch(postalCode))
        {
            Fail("shipping.postalCode", "Enter a valid postal code.");
        }
        if (!Countries.Contains(country, StringComparer.Ordinal))
        {
            Fail("shipping.country", "Choose a country from the list.");
        }
        Required("shipping.phone", "Phone", phone, 6, 30);
        if (phone.Length > 0 && !PhonePattern().IsMatch(phone))
        {
            Fail("shipping.phone", "Enter a valid phone number.");
        }
        if (email.Length == 0 || email.Length > 254 || !MailAddress.TryCreate(email, out _))
        {
            Fail("shipping.email", "Enter a valid email address.");
        }

        var p = request.Payment ?? new PaymentInput(null, null, null, null);
        var number = NonDigits().Replace(p.CardNumber ?? "", "");
        if (number.Length is < 12 or > 19 || !number.All(char.IsAsciiDigit) || !Luhn(number))
        {
            Fail("payment.cardNumber", "Enter a valid card number.");
        }

        var (month, year) = (0, 0);
        var expiry = ExpiryPattern().Match(Text(p.Expiry));
        if (!expiry.Success)
        {
            Fail("payment.expiry", "Enter the expiry date as MM/YY.");
        }
        else
        {
            month = int.Parse(expiry.Groups["m"].Value, System.Globalization.CultureInfo.InvariantCulture);
            year = 2000 + int.Parse(expiry.Groups["y"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var expiresEnd = new DateTimeOffset(year, Math.Clamp(month, 1, 12), 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
            if (month is < 1 or > 12)
            {
                Fail("payment.expiry", "Enter a valid month.");
            }
            else if (expiresEnd <= now)
            {
                Fail("payment.expiry", "This card has expired.");
            }
        }

        var cvc = Text(p.Cvc);
        if (!CvcPattern().IsMatch(cvc))
        {
            Fail("payment.cvc", "Enter the 3 or 4 digit security code.");
        }
        var nameOnCard = Text(p.NameOnCard);
        Required("payment.nameOnCard", "Name on card", nameOnCard, 2, 100);

        if (errors.Count > 0)
        {
            return (null, null, null, errors);
        }

        var address = new ShippingAddress(fullName, line1, line2.Length > 0 ? line2 : null, city, postalCode, country, phone);
        return (address, email, new CardDetails(number, month, year, cvc, nameOnCard), errors);

        void Required(string field, string label, string value, int min, int max)
        {
            if (value.Length == 0)
            {
                Fail(field, $"{label} is required.");
            }
            else if (value.Length < min || value.Length > max)
            {
                Fail(field, $"{label} must be {min}–{max} characters.");
            }
        }
    }

    internal static bool Luhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }
            sum += digit;
        }
        return sum % 10 == 0;
    }

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^(?<m>\d{1,2})\s*/\s*(?<y>\d{2})$")]
    private static partial Regex ExpiryPattern();

    [GeneratedRegex(@"^\d{3,4}$")]
    private static partial Regex CvcPattern();

    [GeneratedRegex(@"^[A-Za-z0-9 -]{3,20}$")]
    private static partial Regex PostalCodePattern();

    [GeneratedRegex(@"^\+?[0-9 ()-]{6,30}$")]
    private static partial Regex PhonePattern();
}
