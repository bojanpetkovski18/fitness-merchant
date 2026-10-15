using System.Globalization;
using System.Text;
using System.Text.Json;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>
/// CSV for spreadsheets (SPEC F16). RFC 4180 quoting, and formula injection is neutralized: a text cell starting
/// with <c>= + - @</c>, tab or carriage return gets a leading <c>'</c>, so a spreadsheet shows it as text instead
/// of running it (a customer could type <c>=HYPERLINK(…)</c> as their name).
/// </summary>
public static class Csv
{
    public static string Text(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }
        return text.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;
    }

    /// <summary>Numbers are written as numbers (invariant culture), never neutralized: they are ours, not user input.</summary>
    public static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed class ExportOptions
{
    public const string Section = "Export";

    /// <summary>Above this the export is refused (400) and the admin narrows the range.</summary>
    public int MaxRows { get; init; } = 10_000;
}

public sealed class ExportOrdersHandler(AppDbContext db, TimeProvider time, Microsoft.Extensions.Options.IOptions<ExportOptions> options)
{
    private static readonly string[] Header =
        ["order_number", "placed_at_utc", "status", "customer_name", "contact_email", "country", "city", "items", "subtotal", "shipping", "total", "currency"];

    public async Task<IResult> ExecuteAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        var (range, errors) = DateRange.Parse(from, to, time.GetUtcNow(), required: true);
        if (range is null)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        var orders = db.Orders.AsNoTracking().Where(o => o.PlacedAt >= range.Start && o.PlacedAt < range.EndExclusive);
        var count = await orders.CountAsync(cancellationToken);
        var max = options.Value.MaxRows;
        if (count > max)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"This range has {count} orders; an export can hold at most {max}. Choose a shorter range.",
                extensions: new Dictionary<string, object?> { ["code"] = "too-many-rows", ["rowCount"] = count, ["maxRows"] = max });
        }

        var rows = orders
            .OrderBy(o => o.PlacedAt).ThenBy(o => o.Id)
            .Select(o => new
            {
                o.Number, o.PlacedAt, o.Status, o.Shipping.FullName, o.ContactEmail, o.Shipping.Country, o.Shipping.City,
                Items = o.Items.Sum(i => i.Quantity), o.Subtotal, o.ShippingCost, o.Total, o.Currency,
            })
            .AsAsyncEnumerable();

        var fileName = $"orders-{range.From:yyyy-MM-dd}-to-{range.To:yyyy-MM-dd}.csv";
        // Streamed row by row; the BOM makes Excel read the file as UTF-8 (names with accents).
        return Results.Stream(async stream =>
        {
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)) { NewLine = "\r\n" };
            await writer.WriteLineAsync(string.Join(',', Header));
            await foreach (var o in rows.WithCancellation(cancellationToken))
            {
                await writer.WriteLineAsync(string.Join(',',
                    Csv.Text(o.Number),
                    o.PlacedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    JsonNamingPolicy.KebabCaseLower.ConvertName(o.Status.ToString()),
                    Csv.Text(o.FullName),
                    Csv.Text(o.ContactEmail),
                    Csv.Text(o.Country),
                    Csv.Text(o.City),
                    Csv.Number(o.Items),
                    Csv.Number(o.Subtotal),
                    Csv.Number(o.ShippingCost),
                    Csv.Number(o.Total),
                    Csv.Text(o.Currency)));
            }
        }, "text/csv; charset=utf-8", fileName);
    }
}
