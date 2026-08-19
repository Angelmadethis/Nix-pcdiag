using PCDiag.Dns;

namespace PCDiag.Tests.Dns;

public class DnsMessageTests
{
    [Fact]
    public void BuildQuery_ShouldProduce12ByteHeaderAndQuestion()
    {
        var query = DnsMessage.BuildQuery("example.com", out _);

        Assert.Equal(12 + 1 + 7 + 1 + 3 + 1 + 4, query.Length); // header + root + example + com + root + QT+QC
        Assert.Equal(0x01, query[5]);   // RD flag in high byte's low nibble
        Assert.Equal(0x00, query[4]);
    }

    [Fact]
    public void BuildQuery_ShouldEncodeMultiLabelName()
    {
        var query = DnsMessage.BuildQuery("www.example.org", out _);

        // header(12) www(4) example(8) org(4) root(1) type(2) class(2)
        Assert.Equal(12 + 4 + 8 + 4 + 1 + 4, query.Length);
        Assert.Equal(3, query[12]);          // length of "www"
        Assert.Equal((byte)'w', query[13]);
        Assert.Equal(7, query[16]);          // length of "example"
        Assert.Equal(3, query[24]);          // length of "org"
        Assert.Equal(0, query[28]);          // root terminator
    }

    [Fact]
    public void BuildQuery_ShouldUseDistinctTransactionIds()
    {
        var a = DnsMessage.BuildQuery("example.com", out _);
        var b = DnsMessage.BuildQuery("example.com", out _);

        Assert.NotEqual((a[0] << 8) | a[1], (b[0] << 8) | b[1]);
    }

    [Fact]
    public void BuildQuery_EmptyDomain_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => DnsMessage.BuildQuery("", out _));
    }

    [Fact]
    public void ParseResponse_ValidResponse_ShouldMatchIdAndRcodeZero()
    {
        var query = DnsMessage.BuildQuery("example.com", out var id);
        var response = CraftResponse(query, id, rcode: 0, answers: 2, setQr: true);

        var parsed = DnsMessage.ParseResponse(response, id);

        Assert.True(parsed.WellFormed);
        Assert.True(parsed.MatchesId);
        Assert.Equal(0, parsed.RCode);
        Assert.Equal(2, parsed.AnswerCount);
    }

    [Fact]
    public void ParseResponse_MismatchedId_ShouldNotMatch()
    {
        var query = DnsMessage.BuildQuery("example.com", out var id);
        var response = CraftResponse(query, (ushort)(id + 1), rcode: 0, answers: 1, setQr: true);

        var parsed = DnsMessage.ParseResponse(response, id);

        Assert.True(parsed.WellFormed);
        Assert.False(parsed.MatchesId);
    }

    [Fact]
    public void ParseResponse_NonZeroRcode_ShouldBeParsed()
    {
        var query = DnsMessage.BuildQuery("example.com", out var id);
        var response = CraftResponse(query, id, rcode: 3, answers: 0, setQr: true);

        var parsed = DnsMessage.ParseResponse(response, id);

        Assert.True(parsed.WellFormed);
        Assert.True(parsed.MatchesId);
        Assert.Equal(3, parsed.RCode);
        Assert.Equal(0, parsed.AnswerCount);
    }

    [Fact]
    public void ParseResponse_QueryPacket_ShouldNotBeWellFormedResponse()
    {
        var query = DnsMessage.BuildQuery("example.com", out var id);

        var parsed = DnsMessage.ParseResponse(query, id);

        Assert.False(parsed.WellFormed);
    }

    [Fact]
    public void ParseResponse_TruncatedPacket_ShouldBeInvalid()
    {
        var parsed = DnsMessage.ParseResponse(new byte[] { 0, 1, 0, 0, 0, 1 }, 1);

        Assert.False(parsed.WellFormed);
        Assert.False(parsed.MatchesId);
    }

    [Fact]
    public void ParseResponse_NullPacket_ShouldBeInvalid()
    {
        var parsed = DnsMessage.ParseResponse(null!, 1);

        Assert.False(parsed.WellFormed);
    }

    [Fact]
    public void ParseResponse_CompressedQuestionName_ShouldStillParse()
    {
        var query = DnsMessage.BuildQuery("example.com", out var id);
        // Simulate a response whose question section uses a compression pointer.
        var response = new byte[12 + 2 + 4 + 4];
        query.AsSpan(0, 12).CopyTo(response);
        response[2] |= 0x80;         // QR
        response[6] = 0;             // ANCOUNT high
        response[7] = 1;             // ANCOUNT low
        response[12] = 0xC0;         // pointer (0b11)
        response[13] = 12;           // points back to offset 12
        response[14] = 0;            // QTYPE high (A)
        response[15] = 1;            // QTYPE low
        response[16] = 0;            // QCLASS high
        response[17] = 1;            // QCLASS low

        var parsed = DnsMessage.ParseResponse(response, id);

        Assert.True(parsed.WellFormed);
        Assert.True(parsed.MatchesId);
    }

    private static byte[] CraftResponse(byte[] query, ushort id, int rcode, int answers, bool setQr)
    {
        var response = (byte[])query.Clone();
        response[0] = (byte)(id >> 8);
        response[1] = (byte)id;
        response[2] = setQr ? (byte)0x80 : (byte)0x00;
        response[3] = (byte)rcode;
        response[6] = (byte)(answers >> 8);
        response[7] = (byte)answers;
        return response;
    }
}