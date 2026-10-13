using System.Security.Cryptography;
using System.Text;

namespace FitnessMerchant.Api.Infrastructure.Seed;

/// <summary>
/// Deterministic IDs for seed rows (RFC 9562 version 8, name-based on SHA-256), so a re-seeded database
/// has the same IDs every time. Rows created at runtime use <see cref="Guid.CreateVersion7()"/>.
/// </summary>
public static class SeedIds
{
    public static Guid For(string key)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes("fitness-merchant-seed:" + key), hash);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }
}
