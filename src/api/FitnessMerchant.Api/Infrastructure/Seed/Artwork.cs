using System.Globalization;
using System.Security;
using System.Text;

namespace FitnessMerchant.Api.Infrastructure.Seed;

public enum ArtworkKind
{
    Tub,
    Jar,
    Bottle,
    Shaker,
    Barbell,
    Dumbbell,
    Kettlebell,
    Plate,
    Bench,
    Treadmill,
    Bike,
    Rower,
    Band,
    Shirt,
    Hoodie,
    Shorts,
    Mat,
    Bag,
}

/// <param name="Label">Large text on packaging, or the weight on equipment.</param>
/// <param name="Sublabel">Small second line (flavour, size).</param>
/// <param name="Fill">Overrides the brand colour for the object itself (apparel and equipment colour variants).</param>
public sealed record ArtworkSpec(ArtworkKind Kind, string Label = "", string Sublabel = "", string? Fill = null);

/// <summary>
/// Original product illustrations and brand wordmarks for the seed catalog (D002), drawn as SVG.
/// Plain shapes and text only: no scripts, styles or external references, so the files are safe to serve.
/// </summary>
public static class Artwork
{
    public const int Size = 800;
    private const string Dark = "#18181b";
    private const string Steel = "#a1a1aa";
    private const string Font = "Arial Black, Arial, Helvetica, sans-serif";

    public static string Product(ArtworkSpec spec, string brandName, string accent)
    {
        var fill = spec.Fill ?? accent;
        var body = spec.Kind switch
        {
            ArtworkKind.Tub => Package(spec, brandName, accent, lid: (240, 215, 320, 70), box: (225, 270, 350, 390, 36), label: (360, 200)),
            ArtworkKind.Jar => Package(spec, brandName, accent, lid: (270, 285, 260, 60), box: (255, 335, 290, 320, 30), label: (400, 180)),
            ArtworkKind.Bottle => Bottle(spec, brandName, accent),
            ArtworkKind.Shaker => Shaker(brandName, accent),
            ArtworkKind.Barbell => Barbell(fill),
            ArtworkKind.Dumbbell => Dumbbell(fill),
            ArtworkKind.Kettlebell => Kettlebell(spec, fill),
            ArtworkKind.Plate => Plate(spec, fill),
            ArtworkKind.Bench => Bench(fill),
            ArtworkKind.Treadmill => Treadmill(fill),
            ArtworkKind.Bike => Bike(fill),
            ArtworkKind.Rower => Rower(fill),
            ArtworkKind.Band => Band(spec, fill),
            ArtworkKind.Shirt => Shirt(brandName, fill, accent),
            ArtworkKind.Hoodie => Hoodie(brandName, fill, accent),
            ArtworkKind.Shorts => Shorts(fill, accent),
            ArtworkKind.Mat => Mat(fill),
            ArtworkKind.Bag => Bag(brandName, fill, accent),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "Unknown artwork kind."),
        };

        var background = Mix(accent, "#ffffff", 0.88);
        var backgroundLow = Mix(accent, "#ffffff", 0.78);
        return
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {Size} {Size}" width="{Size}" height="{Size}">""" +
            $"""<defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{background}"/><stop offset="1" stop-color="{backgroundLow}"/></linearGradient></defs>""" +
            $"""<rect width="{Size}" height="{Size}" fill="url(#bg)"/>""" +
            body +
            "</svg>";
    }

    public static string BrandLogo(string brandName, string accent)
    {
        var text = Escape(brandName.ToUpperInvariant());
        // Arial Black capitals are roughly 0.78em wide; squeeze long names into the available width.
        var fontSize = 52;
        var fit = brandName.Length * fontSize * 0.78 > 470 ? """ textLength="470" lengthAdjust="spacingAndGlyphs" """ : " ";
        return
            """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 200" width="600" height="200">""" +
            $"""<rect width="600" height="200" rx="28" fill="{Dark}"/>""" +
            $"""<rect x="40" y="56" width="16" height="88" rx="4" fill="{accent}"/>""" +
            $"""<text x="80" y="119"{fit}font-family="{Font}" font-size="{fontSize}" font-weight="900" font-style="italic" fill="#ffffff">{text}</text>""" +
            "</svg>";
    }

    private static string Package(ArtworkSpec spec, string brand, string accent,
        (int X, int Y, int W, int H) lid, (int X, int Y, int W, int H, int R) box, (int Y, int H) label)
    {
        var center = box.X + (box.W / 2);
        return
            Shadow(center, box.Y + box.H + 6, box.W * 0.55) +
            $"""<rect x="{box.X}" y="{box.Y}" width="{box.W}" height="{box.H}" rx="{box.R}" fill="{accent}"/>""" +
            $"""<rect x="{lid.X}" y="{lid.Y}" width="{lid.W}" height="{lid.H}" rx="16" fill="{Dark}"/>""" +
            $"""<rect x="{box.X}" y="{label.Y}" width="{box.W}" height="{label.H}" fill="#ffffff"/>""" +
            LabelText(center, label.Y, label.H, box.W - 40, brand, spec, accent);
    }

    private static string Bottle(ArtworkSpec spec, string brand, string accent) =>
        Shadow(400, 666, 140) +
        $"""<rect x="345" y="250" width="110" height="40" fill="{Mix(accent, Dark, 0.3)}"/>""" +
        $"""<rect x="280" y="280" width="240" height="380" rx="44" fill="{accent}"/>""" +
        $"""<rect x="330" y="190" width="140" height="70" rx="12" fill="{Dark}"/>""" +
        $"""<rect x="280" y="370" width="240" height="210" fill="#ffffff"/>""" +
        LabelText(400, 370, 210, 210, brand, spec, accent);

    private static string LabelText(int center, int top, int height, int width, string brand, ArtworkSpec spec, string accent)
    {
        var builder = new StringBuilder();
        builder.Append(Text(center, top + (height * 0.24), 24, Dark, brand.ToUpperInvariant(), width));
        if (spec.Label.Length > 0)
        {
            var size = spec.Label.Length > 7 ? 40 : 56;
            builder.Append(Text(center, top + (height * 0.6), size, accent, spec.Label, width));
        }
        if (spec.Sublabel.Length > 0)
        {
            builder.Append(Text(center, top + (height * 0.85), 22, "#3f3f46", spec.Sublabel, width));
        }
        return builder.ToString();
    }

    private static string Shaker(string brand, string accent) =>
        Shadow(400, 690, 150) +
        $"""<path d="M265 300 L535 300 L505 662 Q502 684 480 684 L320 684 Q298 684 295 662 Z" fill="{accent}"/>""" +
        """<path d="M300 380 H340 M302 440 H342 M305 500 H345" stroke="#ffffff" stroke-opacity="0.7" stroke-width="8" stroke-linecap="round"/>""" +
        $"""<rect x="250" y="240" width="300" height="72" rx="18" fill="{Dark}"/>""" +
        $"""<rect x="360" y="196" width="80" height="54" rx="12" fill="{Dark}"/>""" +
        Text(415, 600, 26, "#ffffff", brand.ToUpperInvariant(), 180);

    private static string Barbell(string accent) =>
        Shadow(400, 470, 330) +
        $"""<rect x="60" y="390" width="680" height="20" rx="10" fill="{Steel}"/>""" +
        """<rect x="60" y="378" width="150" height="44" rx="10" fill="#d4d4d8"/><rect x="590" y="378" width="150" height="44" rx="10" fill="#d4d4d8"/>""" +
        $"""<rect x="205" y="362" width="22" height="76" rx="6" fill="{Dark}"/><rect x="573" y="362" width="22" height="76" rx="6" fill="{Dark}"/>""" +
        $"""<rect x="370" y="386" width="60" height="28" rx="6" fill="{accent}"/>""";

    private static string Dumbbell(string accent) =>
        Shadow(400, 560, 260) +
        $"""<rect x="240" y="386" width="320" height="28" rx="14" fill="{Steel}"/>""" +
        $"""<rect x="170" y="290" width="76" height="220" rx="18" fill="{Dark}"/><rect x="132" y="322" width="48" height="156" rx="14" fill="{Dark}"/>""" +
        $"""<rect x="554" y="290" width="76" height="220" rx="18" fill="{Dark}"/><rect x="620" y="322" width="48" height="156" rx="14" fill="{Dark}"/>""" +
        $"""<rect x="236" y="300" width="12" height="200" rx="4" fill="{accent}"/><rect x="552" y="300" width="12" height="200" rx="4" fill="{accent}"/>""";

    private static string Kettlebell(ArtworkSpec spec, string accent) =>
        Shadow(400, 700, 180) +
        $"""<path d="M290 380 Q285 205 400 205 Q515 205 510 380" fill="none" stroke="{Dark}" stroke-width="48" stroke-linecap="round"/>""" +
        $"""<circle cx="400" cy="500" r="190" fill="{Dark}"/>""" +
        $"""<circle cx="400" cy="520" r="96" fill="{accent}"/>""" +
        Text(400, 540, 64, "#ffffff", spec.Label, 170) +
        Text(400, 585, 24, "#ffffff", spec.Sublabel, 170);

    private static string Plate(ArtworkSpec spec, string accent) =>
        Shadow(400, 700, 220) +
        $"""<circle cx="400" cy="400" r="280" fill="{Dark}"/>""" +
        $"""<circle cx="400" cy="400" r="232" fill="none" stroke="{accent}" stroke-width="12"/>""" +
        $"""<circle cx="400" cy="400" r="74" fill="{Steel}"/><circle cx="400" cy="400" r="28" fill="{Mix(accent, "#ffffff", 0.88)}"/>""" +
        Text(400, 230, 44, "#ffffff", spec.Label, 300) +
        Text(400, 600, 26, accent, spec.Sublabel, 300);

    private static string Bench(string accent) =>
        Shadow(400, 650, 280) +
        $"""<rect x="230" y="468" width="340" height="24" rx="10" fill="{accent}"/>""" +
        $"""<rect x="210" y="440" width="30" height="190" rx="8" fill="{accent}"/><rect x="560" y="440" width="30" height="190" rx="8" fill="{accent}"/>""" +
        $"""<rect x="165" y="616" width="120" height="26" rx="10" fill="{Dark}"/><rect x="515" y="616" width="120" height="26" rx="10" fill="{Dark}"/>""" +
        $"""<rect x="170" y="400" width="300" height="52" rx="22" fill="{Dark}"/>""" +
        $"""<path d="M455 452 L610 250 Q624 232 642 246 L652 254 Q668 268 655 286 L505 470 Z" fill="{Dark}"/>""";

    private static string Treadmill(string accent) =>
        Shadow(400, 652, 290) +
        $"""<path d="M140 598 L660 598 L638 642 L162 642 Z" fill="{Dark}"/>""" +
        """<rect x="160" y="572" width="470" height="30" rx="14" fill="#3f3f46"/>""" +
        $"""<rect x="572" y="300" width="30" height="290" rx="12" fill="{accent}"/>""" +
        $"""<rect x="470" y="330" width="120" height="16" rx="8" fill="{Steel}"/>""" +
        $"""<rect x="500" y="250" width="180" height="70" rx="16" fill="{Dark}"/><rect x="530" y="268" width="120" height="32" rx="6" fill="{accent}"/>""";

    private static string Bike(string accent) =>
        Shadow(400, 672, 270) +
        $"""<circle cx="270" cy="540" r="112" fill="{Dark}"/><circle cx="270" cy="540" r="58" fill="{accent}"/>""" +
        $"""<path d="M270 540 L420 330 L520 580 M520 580 L560 300" fill="none" stroke="{Dark}" stroke-width="30" stroke-linecap="round" stroke-linejoin="round"/>""" +
        $"""<rect x="372" y="300" width="116" height="28" rx="14" fill="{Dark}"/><rect x="520" y="276" width="120" height="24" rx="12" fill="{Dark}"/>""" +
        $"""<rect x="160" y="650" width="220" height="22" rx="11" fill="{Steel}"/><rect x="450" y="650" width="200" height="22" rx="11" fill="{Steel}"/>""";

    private static string Rower(string accent) =>
        Shadow(400, 664, 300) +
        $"""<rect x="110" y="560" width="580" height="26" rx="13" fill="{Steel}"/>""" +
        $"""<rect x="128" y="586" width="26" height="62" rx="8" fill="{Dark}"/><rect x="600" y="586" width="26" height="62" rx="8" fill="{Dark}"/>""" +
        $"""<circle cx="620" cy="490" r="104" fill="{Dark}"/><circle cx="620" cy="490" r="56" fill="{accent}"/>""" +
        $"""<rect x="250" y="518" width="130" height="38" rx="16" fill="{Dark}"/>""" +
        $"""<rect x="486" y="462" width="34" height="98" rx="10" fill="{accent}"/>""";

    private static string Band(ArtworkSpec spec, string fill) =>
        Shadow(400, 612, 240) +
        $"""<ellipse cx="400" cy="420" rx="232" ry="150" fill="none" stroke="{fill}" stroke-width="52"/>""" +
        """<ellipse cx="400" cy="420" rx="232" ry="150" fill="none" stroke="#ffffff" stroke-opacity="0.28" stroke-width="8"/>""" +
        Text(400, 430, 40, Dark, spec.Label, 300) +
        Text(400, 472, 22, "#3f3f46", spec.Sublabel, 300);

    private static string Shirt(string brand, string fill, string accent) =>
        Shadow(400, 672, 200) +
        $"""<path d="M272 222 L344 190 Q400 238 456 190 L528 222 L640 300 L590 384 L540 352 L540 652 L260 652 L260 352 L210 384 L160 300 Z" fill="{fill}"/>""" +
        $"""<path d="M344 190 Q400 238 456 190" fill="none" stroke="{Mix(fill, Dark, 0.35)}" stroke-width="10"/>""" +
        Text(400, 372, 26, Contrast(fill, accent), Initials(brand), 200);

    private static string Hoodie(string brand, string fill, string accent) =>
        Shadow(400, 676, 230) +
        $"""<path d="M300 200 Q400 120 500 200 L560 236 L650 560 L590 584 L540 372 L540 652 L260 652 L260 372 L210 584 L150 560 L240 236 Z" fill="{fill}"/>""" +
        $"""<path d="M330 210 Q400 300 470 210" fill="none" stroke="{Mix(fill, Dark, 0.4)}" stroke-width="12"/>""" +
        $"""<rect x="320" y="520" width="160" height="90" rx="18" fill="{Mix(fill, Dark, 0.18)}"/>""" +
        Text(400, 420, 26, Contrast(fill, accent), Initials(brand), 200);

    private static string Shorts(string fill, string accent) =>
        Shadow(400, 622, 200) +
        $"""<path d="M250 262 L550 262 L582 600 L432 612 L400 400 L368 612 L218 600 Z" fill="{fill}"/>""" +
        $"""<rect x="246" y="236" width="308" height="42" rx="10" fill="{Mix(fill, Dark, 0.4)}"/>""" +
        $"""<rect x="226" y="560" width="10" height="38" fill="{accent}"/><rect x="564" y="560" width="10" height="38" fill="{accent}"/>""";

    private static string Mat(string fill) =>
        Shadow(420, 592, 250) +
        $"""<rect x="190" y="400" width="440" height="160" rx="24" fill="{fill}"/>""" +
        $"""<circle cx="190" cy="480" r="80" fill="{Mix(fill, Dark, 0.25)}"/>""" +
        $"""<circle cx="190" cy="480" r="54" fill="none" stroke="{Mix(fill, "#ffffff", 0.35)}" stroke-width="8"/>""" +
        $"""<circle cx="190" cy="480" r="28" fill="none" stroke="{Mix(fill, "#ffffff", 0.35)}" stroke-width="8"/>""";

    private static string Bag(string brand, string fill, string accent) =>
        Shadow(400, 612, 250) +
        $"""<path d="M300 330 Q300 226 400 226 Q500 226 500 330" fill="none" stroke="{Dark}" stroke-width="24" stroke-linecap="round"/>""" +
        $"""<rect x="170" y="330" width="460" height="262" rx="112" fill="{Dark}"/>""" +
        $"""<rect x="170" y="430" width="460" height="46" fill="{fill}"/>""" +
        Text(400, 464, 26, Contrast(fill, accent), brand.ToUpperInvariant(), 380);

    private static string Shadow(int cx, int cy, double rx) =>
        $"""<ellipse cx="{cx}" cy="{cy}" rx="{N(rx)}" ry="22" fill="#000000" fill-opacity="0.12"/>""";

    private static string Text(double x, double y, int size, string fill, string content, int maxWidth)
    {
        if (content.Length == 0)
        {
            return "";
        }

        var fit = content.Length * size * 0.78 > maxWidth ? $""" textLength="{maxWidth}" lengthAdjust="spacingAndGlyphs" """ : " ";
        return $"""<text x="{N(x)}" y="{N(y)}" text-anchor="middle"{fit}font-family="{Font}" font-size="{size}" font-weight="900" fill="{fill}">{Escape(content)}</text>""";
    }

    private static string Initials(string brand) =>
        string.Concat(brand.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => w[0])).ToUpperInvariant();

    /// <summary>White text on dark fills, the brand accent on light ones.</summary>
    private static string Contrast(string fill, string accent) => Luminance(fill) < 0.4 ? "#ffffff" : Mix(accent, Dark, 0.35);

    private static string Escape(string text) => SecurityElement.Escape(text);

    private static string N(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    internal static string Mix(string from, string to, double amount)
    {
        var (r1, g1, b1) = Rgb(from);
        var (r2, g2, b2) = Rgb(to);
        int Channel(int a, int b) => (int)Math.Round(a + ((b - a) * amount));
        return $"#{Channel(r1, r2):x2}{Channel(g1, g2):x2}{Channel(b1, b2):x2}";
    }

    private static double Luminance(string hex)
    {
        var (r, g, b) = Rgb(hex);
        return ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)) / 255;
    }

    private static (int R, int G, int B) Rgb(string hex) => (
        int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
