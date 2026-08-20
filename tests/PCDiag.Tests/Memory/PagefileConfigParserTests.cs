using PCDiag.Memory;

namespace PCDiag.Tests.Memory;

public class PagefileConfigParserTests
{
    [Fact]
    public void Null_ShouldBeUnavailable()
    {
        var config = PagefileConfigParser.Parse(null);

        Assert.False(config.Available);
        Assert.False(config.IsSystemManaged);
    }

    [Fact]
    public void EmptyInput_ShouldMeanNoPagefile()
    {
        var config = PagefileConfigParser.Parse(Array.Empty<string>());

        Assert.True(config.Available);
        Assert.False(config.IsSystemManaged);
        Assert.Empty(config.Entries);
    }

    [Theory]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\pagefile.sys 0 0")]
    [InlineData(@"?:\pagefile.sys")]
    public void BarePathOrZeroSizes_ShouldBeSystemManaged(string entry)
    {
        var config = PagefileConfigParser.Parse(new[] { entry });

        Assert.True(config.Available);
        Assert.True(config.IsSystemManaged);
    }

    [Fact]
    public void ExplicitSizes_ShouldNotBeSystemManaged()
    {
        var config = PagefileConfigParser.Parse(new[] { @"C:\pagefile.sys 1024 4096" });

        Assert.True(config.Available);
        Assert.False(config.IsSystemManaged);
    }

    [Fact]
    public void MixedEntriesWithAnyExplicitSize_ShouldNotBeSystemManaged()
    {
        var config = PagefileConfigParser.Parse(new[]
        {
            @"C:\pagefile.sys",
            @"D:\pagefile.sys 512 1024"
        });

        Assert.False(config.IsSystemManaged);
        Assert.Equal(2, config.Entries.Count);
    }

    [Fact]
    public void WhitespaceOnly_ShouldMeanNoPagefile()
    {
        var config = PagefileConfigParser.Parse(new[] { "   ", "" });

        Assert.True(config.Available);
        Assert.False(config.IsSystemManaged);
        Assert.Empty(config.Entries);
    }
}