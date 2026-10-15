namespace FitnessMerchant.Api.Domain.Reviews;

/// <summary>
/// A verified buyer's review (SPEC F11): one per user and product. Title and body are plain text, stored exactly
/// as written; clients escape them when rendering. Hidden reviews stay stored but leave the page and the rating.
/// </summary>
public sealed class Review
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int TitleMin = 3;
    public const int TitleMax = 80;
    public const int BodyMin = 10;
    public const int BodyMax = 2000;

    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public Guid UserId { get; init; }
    public int Rating { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public bool IsHidden { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
