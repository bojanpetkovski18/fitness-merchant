using FitnessMerchant.Api.Domain.Reviews;
using FitnessMerchant.Api.Features.Reviews;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Infrastructure.Seed;

/// <summary>
/// Reviews consistent with the seeded orders (M11): only a customer who bought a product in a paid order reviews
/// it, a few days after ordering, at most once. Deterministic (fixed random seed, orders in number order); texts
/// are invented and generic. Runs once: skipped when any review exists. Ratings are then recomputed by SQL.
/// </summary>
public sealed partial class ReviewSeeder(AppDbContext db, TimeProvider time, ILogger<ReviewSeeder> logger)
{
    /// <summary>About two in three eligible purchases get a review.</summary>
    private const double ReviewShare = 0.65;

    private static readonly int[] RatingWeights = [1, 2, 5, 14, 22]; // 1 to 5 stars

    private static readonly Dictionary<int, (string Title, string Body)[]> Texts = new()
    {
        [5] =
        [
            ("Exactly what I wanted", "Arrived quickly and does exactly what it says. I would buy it again without thinking twice."),
            ("Great quality", "Feels solid and well made. I have used it several times a week for a month and it still looks new."),
            ("Part of my routine now", "Easy to use every day and it fits my training well. Good value for the price."),
            ("Very happy", "Better than I expected from the photos. Packaging was neat and nothing was damaged."),
        ],
        [4] =
        [
            ("Good, small niggle", "Does the job well. Only small complaint is the instructions could be clearer, otherwise great."),
            ("Solid choice", "Good quality for the money. Took a few sessions to get used to it, now I like it a lot."),
            ("Would recommend", "Works well and arrived on time. Not perfect, but I would recommend it to a friend."),
        ],
        [3] =
        [
            ("It is fine", "Does what it should, nothing special. I expected a little more for the price."),
            ("Average", "Okay overall. Some things I like, some I do not. It works for my needs for now."),
        ],
        [2] =
        [
            ("Not for me", "It works, but it did not suit how I train. The size was smaller than I expected."),
        ],
        [1] =
        [
            ("Disappointed", "Did not meet my expectations at all. I contacted support and they were helpful though."),
        ],
    };

    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Reviews.AnyAsync(cancellationToken))
        {
            LogSkipped(logger);
            return false;
        }

        // Only the fictional seed customers (they cannot sign in), never real or demo accounts.
        var purchases = await db.Orders.AsNoTracking()
            .Where(o => ReviewRules.QualifyingStatuses.Contains(o.Status) && o.ContactEmail.EndsWith("@customers.example"))
            .OrderBy(o => o.Number)
            .SelectMany(o => o.Items.Select(i => new { o.UserId, i.ProductId, o.PlacedAt }))
            .ToListAsync(cancellationToken);
        if (purchases.Count == 0)
        {
            return false;
        }

        var now = time.GetUtcNow();
        var random = new Random(20261015);
        var reviews = new List<Review>();
        foreach (var purchase in purchases.DistinctBy(p => (p.UserId, p.ProductId)))
        {
            if (random.NextDouble() > ReviewShare)
            {
                continue;
            }
            var rating = PickRating(random);
            var (title, body) = Texts[rating][random.Next(Texts[rating].Length)];
            var createdAt = purchase.PlacedAt.AddDays(2 + random.Next(10)).AddMinutes(random.Next(600));
            if (createdAt > now)
            {
                continue; // bought too recently to have reviewed it yet
            }
            reviews.Add(new Review
            {
                Id = SeedIds.For($"review:{purchase.UserId}:{purchase.ProductId}"),
                ProductId = purchase.ProductId,
                UserId = purchase.UserId,
                Rating = rating,
                Title = title,
                Body = body,
                CreatedAt = createdAt,
            });
        }

        await using var transaction = await JoinableTransaction.BeginAsync(db, cancellationToken);
        db.Reviews.AddRange(reviews);
        await db.SaveChangesAsync(cancellationToken);
        var productIds = reviews.Select(r => r.ProductId).Distinct().ToList();
        foreach (var productId in productIds)
        {
            await ReviewRules.RecalculateAsync(db, productId, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        LogSeeded(logger, reviews.Count, productIds.Count);
        return true;
    }

    private static int PickRating(Random random)
    {
        var roll = random.Next(RatingWeights.Sum());
        for (var stars = 1; stars <= 5; stars++)
        {
            roll -= RatingWeights[stars - 1];
            if (roll < 0)
            {
                return stars;
            }
        }
        return 5;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {ReviewCount} reviews on {ProductCount} products")]
    private static partial void LogSeeded(ILogger logger, int reviewCount, int productCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Review seed skipped: reviews already exist")]
    private static partial void LogSkipped(ILogger logger);
}
