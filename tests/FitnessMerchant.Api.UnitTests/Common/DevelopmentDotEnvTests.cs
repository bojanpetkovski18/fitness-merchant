using FitnessMerchant.Api.Common;

namespace FitnessMerchant.Api.UnitTests.Common;

public class DevelopmentDotEnvTests
{
    [Fact]
    public void Parses_keys_into_configuration_paths_and_skips_comments_and_blank_lines()
    {
        var values = DevelopmentDotEnv.Parse(
        [
            "# comment",
            "",
            "ConnectionStrings__FmTestDb=Host=localhost;Port=5433;Password=a=b",
            "Seed__FmTestEnabled = true ",
            "not-a-setting",
        ]);

        Assert.Equal("Host=localhost;Port=5433;Password=a=b", values["ConnectionStrings:FmTestDb"]);
        Assert.Equal("true", values["Seed:FmTestEnabled"]);
        Assert.Equal(2, values.Count);
    }

    [Theory]
    [InlineData("\"quoted value\"", "quoted value")]
    [InlineData("'single'", "single")]
    [InlineData("\"unbalanced'", "\"unbalanced'")]
    public void Strips_matching_surrounding_quotes(string raw, string expected)
    {
        var values = DevelopmentDotEnv.Parse([$"FM_TEST_QUOTES={raw}"]);

        Assert.Equal(expected, values["FM_TEST_QUOTES"]);
    }

    [Fact]
    public void Real_environment_variables_win_over_the_file()
    {
        Environment.SetEnvironmentVariable("FM_TEST_REAL_ENV_WINS", "from-environment");
        try
        {
            var values = DevelopmentDotEnv.Parse(["FM_TEST_REAL_ENV_WINS=from-file"]);

            Assert.False(values.ContainsKey("FM_TEST_REAL_ENV_WINS"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FM_TEST_REAL_ENV_WINS", null);
        }
    }
}
