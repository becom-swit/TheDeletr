using Thedeletr.Cli;

namespace Thedeletr.Tests;

public class CommandBuilderTests
{
    [Theory]
    [InlineData("2024-12-31", 2024, 12, 31, 0, 0)]
    [InlineData("2024-12-31 18:30", 2024, 12, 31, 18, 30)]
    [InlineData("2024-12-31T18:30:00", 2024, 12, 31, 18, 30)]
    public void TryParseDate_IsoFormats(string value, int y, int m, int d, int h, int min)
    {
        Assert.True(CommandBuilder.TryParseDate(value, out var date));
        Assert.Equal(new DateTime(y, m, d, h, min, 0), date);
    }

    [Fact]
    public void TryParseDate_Invalid() => Assert.False(CommandBuilder.TryParseDate("not a date", out _));

    [Fact]
    public void Parse_AllOptionsAndAliases()
    {
        Options.DeletionOptions? captured = null;
        var command = CommandBuilder.Build((o, _) =>
        {
            captured = o;
            return Task.FromResult(0);
        });

        var exit = command.Parse([Path.GetTempPath(), "-d", "2024-01-01", "-dr", "-e", "a;b", "-e", "c", "-y", "-b", "-v", "-l", "logs"]).Invoke();

        Assert.Equal(0, exit);
        Assert.NotNull(captured);
        Assert.Equal(new DateTime(2024, 1, 1), captured.Cutoff);
        Assert.True(captured.DryRun);
        Assert.Equal(["a;b", "c"], captured.Excluded);
        Assert.True(captured.Yes);
        Assert.True(captured.RecycleBin);
        Assert.True(captured.Verbose);
        Assert.Equal("logs", captured.LogFolder.Name);
    }

    [Fact]
    public void Parse_MissingDate_IsError()
    {
        var command = CommandBuilder.Build((_, _) => Task.FromResult(0));

        Assert.NotEmpty(command.Parse([Path.GetTempPath()]).Errors);
    }

    [Fact]
    public void Parse_NonExistingRoot_IsError()
    {
        var command = CommandBuilder.Build((_, _) => Task.FromResult(0));

        Assert.NotEmpty(command.Parse([Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), "-d", "2024-01-01"]).Errors);
    }
}
