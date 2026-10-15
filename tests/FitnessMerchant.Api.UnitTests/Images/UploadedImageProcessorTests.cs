using System.Text;
using FitnessMerchant.Api.Infrastructure.Images;
using FitnessMerchant.Api.Tests.Images;
using SkiaSharp;

namespace FitnessMerchant.Api.UnitTests.Images;

public class UploadedImageProcessorTests
{
    public static TheoryData<string, byte[]> Uploads() => new()
    {
        { "png", TestImages.Png() },
        { "jpeg", TestImages.Jpeg() },
        { "webp", TestImages.Webp() },
    };

    [Theory]
    [MemberData(nameof(Uploads))]
    public void Png_jpeg_and_webp_are_re_encoded_to_webp_with_their_size(string format, byte[] upload)
    {
        var accepted = Assert.IsType<ProcessedImage.Accepted>(UploadedImageProcessor.Process(upload));

        Assert.Equal(UploadedImageProcessor.Format.Webp, UploadedImageProcessor.Sniff(accepted.Webp));
        Assert.Equal((40, 20), (accepted.Width, accepted.Height));
        Assert.Equal((40, 20), TestImages.Size(accepted.Webp));
        Assert.True(IsReddish(TestImages.PixelAt(accepted.Webp, 5, 10)), format);
    }

    [Fact]
    public void Exif_metadata_is_removed_and_its_orientation_applied_first()
    {
        var upload = TestImages.JpegWithExif(orientation: 6); // "rotate 90° clockwise to display"
        Assert.Contains(TestImages.ExifCamera, Encoding.ASCII.GetString(upload), StringComparison.Ordinal);

        var accepted = Assert.IsType<ProcessedImage.Accepted>(UploadedImageProcessor.Process(upload));

        var text = Encoding.ASCII.GetString(accepted.Webp);
        Assert.DoesNotContain(TestImages.ExifCamera, text, StringComparison.Ordinal);
        Assert.DoesNotContain("EXIF", text, StringComparison.Ordinal); // no WebP EXIF chunk
        Assert.DoesNotContain("XMP ", text, StringComparison.Ordinal);
        // 40×20 with red on the left becomes 20×40 with red on top.
        Assert.Equal((20, 40), (accepted.Width, accepted.Height));
        Assert.True(IsReddish(TestImages.PixelAt(accepted.Webp, 10, 5)));
        Assert.True(IsBluish(TestImages.PixelAt(accepted.Webp, 10, 35)));
    }

    [Fact]
    public void A_text_file_is_rejected_whatever_it_is_called()
    {
        var rejected = Assert.IsType<ProcessedImage.Rejected>(UploadedImageProcessor.Process("hello, this is not a picture"u8.ToArray()));

        Assert.Equal("Upload a PNG, JPEG or WebP image.", rejected.Message);
    }

    [Fact]
    public void A_gif_is_rejected_even_though_the_decoder_could_read_it() =>
        Assert.IsType<ProcessedImage.Rejected>(UploadedImageProcessor.Process(TestImages.Gif()));

    [Fact]
    public void Correct_magic_bytes_on_a_broken_file_are_rejected()
    {
        byte[] fake = [.. TestImages.Png().AsSpan(0, 8), .. "not really a png"u8];

        var rejected = Assert.IsType<ProcessedImage.Rejected>(UploadedImageProcessor.Process(fake));

        Assert.Equal("The file could not be read as an image.", rejected.Message);
    }

    [Fact]
    public void A_truncated_image_is_rejected()
    {
        var png = TestImages.Png(400, 400);

        Assert.IsType<ProcessedImage.Rejected>(UploadedImageProcessor.Process(png[..(png.Length / 2)]));
    }

    [Theory]
    [InlineData(4001, 10)]
    [InlineData(10, 4001)]
    public void Images_over_4000_px_are_rejected_before_decoding(int width, int height)
    {
        var rejected = Assert.IsType<ProcessedImage.Rejected>(UploadedImageProcessor.Process(TestImages.Png(width, height)));

        Assert.Contains("maximum is 4000×4000 px", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exactly_4000_px_is_allowed() =>
        Assert.IsType<ProcessedImage.Accepted>(UploadedImageProcessor.Process(TestImages.Png(4000, 4)));

    [Fact]
    public void Files_over_1_MB_are_rejected()
    {
        var big = new byte[UploadedImageProcessor.MaxBytes + 1];
        TestImages.Png().AsSpan(0, 8).CopyTo(big);

        var rejected = Assert.IsType<ProcessedImage.Rejected>(UploadedImageProcessor.Process(big));

        Assert.Equal("The image is larger than 1 MB.", rejected.Message);
    }

    [Fact]
    public void The_same_upload_always_gives_the_same_bytes_so_it_can_be_deduplicated()
    {
        var upload = TestImages.Jpeg(120, 80);

        var first = Assert.IsType<ProcessedImage.Accepted>(UploadedImageProcessor.Process(upload));
        var second = Assert.IsType<ProcessedImage.Accepted>(UploadedImageProcessor.Process(upload));

        Assert.Equal(first.Webp, second.Webp);
    }

    private static bool IsReddish(SKColor c) => c.Red > 200 && c.Blue < 60;

    private static bool IsBluish(SKColor c) => c.Blue > 200 && c.Red < 60;
}
