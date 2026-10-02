using Thedeletr.Core;

namespace Thedeletr.Tests;

public class DurationFormatterTests
{
    [Theory]
    [InlineData(0, "0 seconds")]
    [InlineData(1, "1 second")]
    [InlineData(59, "59 seconds")]
    [InlineData(60, "01:00")]
    [InlineData(61, "01:01")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "01:00:00")]
    [InlineData(3661, "01:01:01")]
    [InlineData(90061, "25:01:01")]
    public void Format(int seconds, string expected) =>
        Assert.Equal(expected, DurationFormatter.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Format_TruncatesFractions() =>
        Assert.Equal("59 seconds", DurationFormatter.Format(TimeSpan.FromSeconds(59.9)));
}
