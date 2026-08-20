using PCDiag.Net.Tcp;
using PCDiag.Tests.Net.Tcp;

namespace PCDiag.Tests.Net.Tcp;

public class TcpStateMappingTests
{
    [Theory]
    [InlineData(2, TcpConnectionState.Listen)]
    [InlineData(5, TcpConnectionState.Established)]
    [InlineData(8, TcpConnectionState.CloseWait)]
    [InlineData(11, TcpConnectionState.TimeWait)]
    [InlineData(100, TcpConnectionState.Bound)]
    [InlineData(0, TcpConnectionState.Unknown)]
    public void FromMibState_KnownValues_ShouldMap(int value, TcpConnectionState expected)
        => Assert.Equal(expected, TcpConnectionStateExtensions.FromMibState(value));

    [Fact]
    public void FromMibState_UnknownValue_ShouldReturnUnknown()
        => Assert.Equal(TcpConnectionState.Unknown, TcpConnectionStateExtensions.FromMibState(201));

    [Theory]
    [InlineData("Intel[R] Wi-Fi 6 AX201 160MHz", "intelrwifi6ax201160mhz")]
    [InlineData("Intel(R) Wi-Fi 6 AX201 160MHz", "intelrwifi6ax201160mhz")]
    [InlineData("Ethernet", "ethernet")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Normalize_DifferentForms_ShouldMatch(string? input, string? expected)
        => Assert.Equal(expected, WmiTcpAdapterErrorSource.Normalize(input));

    [Theory]
    [InlineData(0, TcpAutotuningLevel.Normal)]
    [InlineData(252, TcpAutotuningLevel.Experimental)]
    [InlineData(253, TcpAutotuningLevel.Restricted)]
    [InlineData(254, TcpAutotuningLevel.HighlyRestricted)]
    [InlineData(255, TcpAutotuningLevel.Disabled)]
    [InlineData(999, TcpAutotuningLevel.Unknown)]
    public void MapAutotuningLevel_KnownValues_ShouldMap(int value, TcpAutotuningLevel expected)
        => Assert.Equal(expected, WmiTcpConfigSource.MapAutotuningLevel(value));
}