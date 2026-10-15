using SkiaSharp;

namespace FitnessMerchant.Api.Infrastructure.Images;

public abstract record ProcessedImage
{
    /// <summary>WebP bytes with no metadata, already turned upright.</summary>
    public sealed record Accepted(byte[] Webp, int Width, int Height) : ProcessedImage;

    public sealed record Rejected(string Message) : ProcessedImage;
}

/// <summary>
/// Turns an untrusted upload into a clean WebP (SPEC section 8, D026). The format comes from the magic bytes,
/// never from the file name or the declared content type. Dimensions are read from the header before any pixels
/// are decoded, so a small file that would decode into a huge bitmap is refused cheaply. Re-encoding from pixels
/// drops EXIF, XMP and ICC metadata (camera, GPS); the EXIF orientation is applied first so photos stay upright.
/// </summary>
public static class UploadedImageProcessor
{
    public const int MaxBytes = 1024 * 1024;
    public const int MaxDimension = 4000;
    public const int WebpQuality = 85;
    public const string ContentType = "image/webp";

    public enum Format
    {
        Png,
        Jpeg,
        Webp,
    }

    public static Format? Sniff(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Format.Png;
        }
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Format.Jpeg;
        }
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return Format.Webp;
        }
        return null;
    }

    public static ProcessedImage Process(byte[] data)
    {
        if (data.Length == 0)
        {
            return new ProcessedImage.Rejected("The file is empty.");
        }
        if (data.Length > MaxBytes)
        {
            return new ProcessedImage.Rejected("The image is larger than 1 MB.");
        }
        if (Sniff(data) is not { } format)
        {
            return new ProcessedImage.Rejected("Upload a PNG, JPEG or WebP image.");
        }

        using var stream = new SKMemoryStream(data);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat != ToSkia(format))
        {
            return new ProcessedImage.Rejected("The file could not be read as an image.");
        }

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width > MaxDimension || height > MaxDimension)
        {
            return new ProcessedImage.Rejected($"The image is {width}×{height} px; the maximum is {MaxDimension}×{MaxDimension} px.");
        }

        using var decoded = new SKBitmap(codec.Info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            return new ProcessedImage.Rejected("The file could not be read as an image.");
        }

        using var upright = Orient(decoded, codec.EncodedOrigin);
        using var image = SKImage.FromBitmap(upright);
        using var webp = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        return new ProcessedImage.Accepted(webp.ToArray(), upright.Width, upright.Height);
    }

    private static SKEncodedImageFormat ToSkia(Format format) => format switch
    {
        Format.Png => SKEncodedImageFormat.Png,
        Format.Jpeg => SKEncodedImageFormat.Jpeg,
        _ => SKEncodedImageFormat.Webp,
    };

    /// <summary>A copy drawn with the EXIF orientation applied (the caller disposes it, the source too).</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var (w, h) = ((float)source.Width, (float)source.Height);
        // Each matrix maps source pixels to their upright position: rows [ScaleX SkewX TransX; SkewY ScaleY TransY].
        var (matrix, swap) = origin switch
        {
            SKEncodedOrigin.TopRight => (Matrix(-1, 0, w, 0, 1, 0), false),     // mirrored horizontally
            SKEncodedOrigin.BottomRight => (Matrix(-1, 0, w, 0, -1, h), false), // rotated 180°
            SKEncodedOrigin.BottomLeft => (Matrix(1, 0, 0, 0, -1, h), false),   // mirrored vertically
            SKEncodedOrigin.LeftTop => (Matrix(0, 1, 0, 1, 0, 0), true),        // transposed
            SKEncodedOrigin.RightTop => (Matrix(0, -1, h, 1, 0, 0), true),      // needs 90° clockwise
            SKEncodedOrigin.RightBottom => (Matrix(0, -1, h, -1, 0, w), true),  // transversed
            SKEncodedOrigin.LeftBottom => (Matrix(0, 1, 0, -1, 0, w), true),    // needs 90° counter-clockwise
            _ => (SKMatrix.Identity, false),
        };

        var result = new SKBitmap(new SKImageInfo(
            swap ? source.Height : source.Width, swap ? source.Width : source.Height, source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(matrix);
        // Exact quarter turns and mirrors: nearest-neighbour copies pixels without blurring.
        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    private static SKMatrix Matrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY) =>
        new() { ScaleX = scaleX, SkewX = skewX, TransX = transX, SkewY = skewY, ScaleY = scaleY, TransY = transY, Persp2 = 1 };
}
