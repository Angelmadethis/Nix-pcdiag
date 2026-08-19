using System.Net;
using PCDiag.Dns;

namespace PCDiag.Tests.Dns;

public class DnsServerSourceTests
{
    [Fact]
    public void ParseAndDedupe_ShouldParseValidAddresses()
    {
        var result = WmiDnsServerSource.ParseAndDedupe(new[] { "192.168.1.1", "8.8.8.8" });

        Assert.Equal(2, result.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), result[0]);
        Assert.Equal(IPAddress.Parse("8.8.8.8"), result[1]);
    }

    [Fact]
    public void ParseAndDedupe_ShouldRemoveDuplicates()
    {
        var result = WmiDnsServerSource.ParseAndDedupe(new[] { "192.168.1.1", "192.168.1.1", "8.8.8.8" });

        Assert.Single(result.Where(a => a.Equals(IPAddress.Parse("192.168.1.1"))));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseAndDedupe_ShouldIgnoreInvalidAndEmpty()
    {
        var result = WmiDnsServerSource.ParseAndDedupe(new[] { "", "  ", "not-an-ip", "10.0.0.1" });

        Assert.Single(result);
        Assert.Equal(IPAddress.Parse("10.0.0.1"), result[0]);
    }

    [Fact]
    public void ParseAndDedupe_ShouldSupportIPv6()
    {
        var result = WmiDnsServerSource.ParseAndDedupe(new[] { "2606:4700:4700::1111" });

        Assert.Single(result);
        Assert.Equal(IPAddress.Parse("2606:4700:4700::1111"), result[0]);
    }

    [Fact]
    public void ParseAndDedupe_ShouldTrimWhitespace()
    {
        var result = WmiDnsServerSource.ParseAndDedupe(new[] { "  8.8.8.8  " });

        Assert.Single(result);
        Assert.Equal(IPAddress.Parse("8.8.8.8"), result[0]);
    }

    [Fact]
    public void ParseAndDedupe_EmptyInput_ShouldReturnEmpty()
    {
        Assert.Empty(WmiDnsServerSource.ParseAndDedupe(Array.Empty<string>()));
    }
}