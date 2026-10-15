using FitnessMerchant.Api.Features.Admin;

namespace FitnessMerchant.Api.UnitTests.Admin;

public class CsvTests
{
    [Theory]
    [InlineData("Ana", "Ana")]
    [InlineData("", "")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+49 000", "'+49 000")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("\rcmd", "\"'\rcmd\"")]          // neutralized, then quoted because of the CR
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("x=1", "x=1")]                   // only a leading sign is dangerous
    public void Text_cells_are_neutralized_then_quoted(string value, string expected) =>
        Assert.Equal(expected, Csv.Text(value));

    [Fact]
    public void Numbers_use_the_invariant_culture_with_two_decimals() =>
        Assert.Equal(("1234.50", "7"), (Csv.Number(1234.5m), Csv.Number(7)));
}
