using System.Globalization;
using System.Text;

namespace FitnessMerchant.Api.Common;

public static class Slugs
{
    /// <summary>"Vitamin D3 + K2 (90 caps)" becomes "vitamin-d3-k2-90-caps": lowercase ASCII letters, digits and single hyphens.</summary>
    public static string Slugify(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingHyphen = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue; // drop accents: "é" -> "e"
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(c));
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return builder.ToString();
    }
}
