namespace FitnessMerchant.Api.Domain.Media;

/// <summary>
/// An image stored in the database (D009) and served from <c>/api/images/{id}</c>.
/// <see cref="Sha256"/> makes uploads content-addressed, so an identical file is stored once.
/// </summary>
public sealed class Image
{
    public Guid Id { get; init; }

    /// <summary>Lowercase hex SHA-256 of <see cref="Data"/>.</summary>
    public required string Sha256 { get; init; }

    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }
    public int SizeBytes { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
