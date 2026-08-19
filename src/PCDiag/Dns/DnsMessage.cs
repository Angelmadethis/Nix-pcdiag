using System.Text;

namespace PCDiag.Dns;

/// <summary>
/// A parsed DNS response relevant to our probes.
/// Only the header and question section are parsed; answer records are counted
/// but not decoded (latency/health does not require their contents).
/// </summary>
public sealed record DnsResponse
{
    /// <summary>Whether the response ID matched the query ID.</summary>
    public bool MatchesId { get; init; }

    /// <summary>Whether the packet was structurally parseable and the QR flag was set.</summary>
    public bool WellFormed { get; init; }

    /// <summary>The response code from the header flags (0 = NOERROR).</summary>
    public int RCode { get; init; }

    /// <summary>The number of answer records in the header.</summary>
    public int AnswerCount { get; init; }

    /// <summary>A response representing a structurally invalid or unmatched packet.</summary>
    public static DnsResponse Invalid { get; } = new();
}

/// <summary>
/// Minimal DNS wire-format builder/parser (RFC 1035) for A-record queries.
/// Used to probe specific resolvers directly over UDP so latency, failures,
/// and timeouts can be measured per server.
/// </summary>
public static class DnsMessage
{
    /// <summary>
    /// Build a standard A-record query (RD set, QCLASS IN) for <paramref name="domain"/>.
    /// The transaction ID is generated per call and returned via <paramref name="queryId"/>.
    /// </summary>
    public static byte[] BuildQuery(string domain, out ushort queryId)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain must not be empty.", nameof(domain));

        queryId = (ushort)Random.Shared.Next(0, 65536);

        var ms = new MemoryStream(64);
        WriteUInt16(ms, queryId);
        WriteUInt16(ms, 0x0100);   // flags: QR=0, RD=1
        WriteUInt16(ms, 1);        // QDCOUNT
        WriteUInt16(ms, 0);        // ANCOUNT
        WriteUInt16(ms, 0);        // NSCOUNT
        WriteUInt16(ms, 0);        // ARCOUNT

        var labels = domain.TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var label in labels)
        {
            if (label.Length > 63)
                throw new ArgumentException($"Label '{label}' exceeds 63 bytes.", nameof(domain));
            ms.WriteByte((byte)label.Length);
            foreach (var ch in label)
                ms.WriteByte((byte)ch);
        }
        ms.WriteByte(0);           // root label

        WriteUInt16(ms, 1);        // QTYPE = A
        WriteUInt16(ms, 1);        // QCLASS = IN
        return ms.ToArray();
    }

    /// <summary>
    /// Parse a response packet and check it against the expected transaction ID.
    /// Malformed/truncated packets yield <see cref="DnsResponse.Invalid"/>.
    /// </summary>
    public static DnsResponse ParseResponse(byte[] packet, ushort expectedId)
    {
        if (packet is null || packet.Length < 12)
            return DnsResponse.Invalid;

        var id = ReadUInt16(packet, 0);
        var flags = ReadUInt16(packet, 2);
        var qr = (flags & 0x8000) != 0;
        var rcode = flags & 0x000F;
        var qdCount = ReadUInt16(packet, 4);
        var anCount = ReadUInt16(packet, 6);

        var offset = 12;
        for (int i = 0; i < qdCount; i++)
        {
            if (!TrySkipName(packet, offset, out var consumed))
                return DnsResponse.Invalid;
            offset += consumed + 4; // QTYPE + QCLASS
            if (offset > packet.Length)
                return DnsResponse.Invalid;
        }

        return new DnsResponse
        {
            MatchesId = id == expectedId,
            WellFormed = qr,
            RCode = rcode,
            AnswerCount = anCount
        };
    }

    /// <summary>
    /// Skip a (possibly compressed) domain name starting at <paramref name="start"/>.
    /// Returns the number of bytes consumed by the name.
    /// </summary>
    private static bool TrySkipName(byte[] packet, int start, out int consumed)
    {
        consumed = 0;
        var offset = start;
        while (offset < packet.Length)
        {
            var len = packet[offset];
            if (len == 0)
            {
                consumed = offset - start + 1;
                return true;
            }
            if ((len & 0xC0) == 0xC0)
            {
                consumed = offset - start + 2;
                return true;
            }
            if ((len & 0xC0) != 0)
                return false;

            offset += 1 + len;
            if (offset >= packet.Length)
                return false;
        }
        return false;
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static ushort ReadUInt16(byte[] packet, int offset)
        => (ushort)((packet[offset] << 8) | packet[offset + 1]);
}