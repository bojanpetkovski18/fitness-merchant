using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace FitnessMerchant.Api.Tests.Images;

/// <summary>
/// Real image files generated in memory for upload tests: the left half is red and the right half blue, so a test
/// can tell whether an image was turned. Shared with the integration tests (linked source file).
/// </summary>
internal static class TestImages
{
    public const string ExifCamera = "SecretCam-9000";

    public static byte[] Png(int width = 40, int height = 20) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width = 40, int height = 20) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    public static byte[] Webp(int width = 40, int height = 20) => Encode(width, height, SKEncodedImageFormat.Webp);

    public static byte[] Gif() => [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0, 0x3B];

    /// <summary>A JPEG with an EXIF block naming a camera and saying "rotate 90° clockwise to display" (orientation 6).</summary>
    public static byte[] JpegWithExif(int width = 40, int height = 20, ushort orientation = 6)
    {
        var jpeg = Jpeg(width, height);
        var exif = ExifSegment(ExifCamera, orientation);
        // Insert APP1 right after the SOI marker (FF D8), where EXIF lives in camera files.
        return [.. jpeg.AsSpan(0, 2), .. exif, .. jpeg.AsSpan(2)];
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, width / 2f, height, red);
            canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }

    /// <summary>APP1 "Exif" segment with a little-endian TIFF header and one IFD: Make (0x010F) and Orientation (0x0112).</summary>
    private static byte[] ExifSegment(string make, ushort orientation)
    {
        var makeBytes = Encoding.ASCII.GetBytes(make + "\0");
        const int ifdOffset = 8;
        const int entries = 2;
        var dataOffset = ifdOffset + 2 + (entries * 12) + 4;

        var tiff = new byte[dataOffset + makeBytes.Length];
        "II*\0"u8.CopyTo(tiff);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4), ifdOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(ifdOffset), entries);

        var entry = tiff.AsSpan(ifdOffset + 2);
        BinaryPrimitives.WriteUInt16LittleEndian(entry, 0x010F);                 // Make
        BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], 2);                 // ASCII
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], (uint)makeBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)dataOffset);

        entry = entry[12..];
        BinaryPrimitives.WriteUInt16LittleEndian(entry, 0x0112);                 // Orientation
        BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], 3);                 // SHORT
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[8..], orientation);

        makeBytes.CopyTo(tiff, dataOffset);                                      // next-IFD offset stays 0

        byte[] payload = [.. "Exif\0\0"u8, .. tiff];
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);
        return segment;
    }

    /// <summary>The colour at a point of an encoded image, for orientation checks.</summary>
    public static SKColor PixelAt(byte[] encoded, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(encoded);
        return bitmap.GetPixel(x, y);
    }

    public static (int Width, int Height) Size(byte[] encoded)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(encoded));
        return (codec.Info.Width, codec.Info.Height);
    }
}
