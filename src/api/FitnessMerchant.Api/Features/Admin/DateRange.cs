using System.Globalization;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>
/// An inclusive range of whole UTC days from <c>from</c>/<c>to</c> query values (<c>yyyy-MM-dd</c>, D027).
/// Missing values default to the last 30 days ending today. As instants it is [From 00:00Z, To+1 00:00Z).
/// </summary>
internal sealed record DateRange(DateOnly From, DateOnly To)
{
    public const int MaxDays = 366;
    public const int DefaultDays = 30;

    public int Days => To.DayNumber - From.DayNumber + 1;
    public DateTimeOffset Start => new(From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    public DateTimeOffset EndExclusive => new(To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public static (DateRange? Range, FieldErrors Errors) Parse(string? from, string? to, DateTimeOffset now, bool required = false)
    {
        var errors = new FieldErrors();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var toDate = ParseDay(to, "to", today, required, errors);
        var fromDate = ParseDay(from, "from", toDate?.AddDays(-(DefaultDays - 1)) ?? today, required, errors);
        if (errors.Any)
        {
            return (null, errors);
        }

        if (fromDate > toDate)
        {
            errors.Add("from", "The start date must be on or before the end date.");
        }
        else if (toDate!.Value.DayNumber - fromDate!.Value.DayNumber + 1 > MaxDays)
        {
            errors.Add("to", $"The range can be at most {MaxDays} days.");
        }
        return errors.Any ? (null, errors) : (new DateRange(fromDate!.Value, toDate!.Value), errors);
    }

    private static DateOnly? ParseDay(string? value, string field, DateOnly fallback, bool required, FieldErrors errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(field, "Choose a date.");
                return null;
            }
            return fallback;
        }
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) || day.Year < 2000 || day.Year > 2100)
        {
            errors.Add(field, "Use a date like 2026-10-15.");
            return null;
        }
        return day;
    }
}
