using Microsoft.Win32;
using PCDiag.Infrastructure;

namespace PCDiag.Net.Tcp;

/// <summary>
/// Reads TCP configuration from the registry (read-only) and from
/// <c>MSFT_NetTCPSetting</c> (auto-tuning level, dynamic port range). Never writes
/// any registry value. Never throws; unset/missing values are reported as null so
/// callers can distinguish "Windows default" from a configured tweak.
/// </summary>
public sealed class WmiTcpConfigSource : ITcpConfigSource
{
    private const string ParametersKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";

    public TcpConfiguration GetConfig()
    {
        var config = new TcpConfiguration
        {
            TcpTimedWaitDelay = ReadDword(ParametersKey, "TcpTimedWaitDelay"),
            TcpNumConnections = ReadDword(ParametersKey, "TcpNumConnections"),
            TcpMaxDataRetransmissions = ReadDword(ParametersKey, "TcpMaxDataRetransmissions"),
            MaxUserPort = ReadDword(ParametersKey, "MaxUserPort"),
            GlobalMaxTcpWindowSize = ReadDword(ParametersKey, "GlobalMaxTcpWindowSize"),
            TcpWindowSize = ReadMaxInterfaceDword("TcpWindowSize")
        };

        ApplyNetTcpSetting(config, out config);
        return config;
    }

    private static void ApplyNetTcpSetting(TcpConfiguration input, out TcpConfiguration output)
    {
        var config = input;
        foreach (var row in WmiQuery.Query(
                     "SELECT SettingName, AutoTuningLevelEffective, AutoTuningLevelGroupPolicy, DynamicPortRangeStartPort, DynamicPortRangeNumberOfPorts FROM MSFT_NetTCPSetting",
                     "root\\StandardCimv2"))
        {
            var settingName = WmiQuery.GetString(row, "SettingName") ?? "";
            if (string.Equals(settingName, "Automatic", StringComparison.OrdinalIgnoreCase))
                continue;

            if (WmiQuery.GetInt32(row, "AutoTuningLevelEffective") is int eff)
                config = config with { AutotuningLevel = MapAutotuningLevel(eff) };

            if (WmiQuery.GetInt32(row, "AutoTuningLevelGroupPolicy") is int gp)
            {
                var mapped = MapAutotuningLevel(gp);
                if (mapped is not (TcpAutotuningLevel.Unknown or TcpAutotuningLevel.HighlyRestricted)
                    && mapped != config.AutotuningLevel)
                    config = config with { AutotuningGroupPolicy = mapped };
            }

            if (config.DynamicPortStart is null && config.DynamicPortCount is null)
            {
                var start = WmiQuery.GetInt32(row, "DynamicPortRangeStartPort");
                var count = WmiQuery.GetInt32(row, "DynamicPortRangeNumberOfPorts");
                if (start is not null && count is not null)
                    config = config with { DynamicPortStart = start, DynamicPortCount = count };
            }
        }

        output = config;
    }

    /// <summary>
    /// Map a raw MSFT_NetTCPSetting auto-tuning value to the friendly enum.
    /// Pure and unit-testable. Values: 0=Normal, 252=Experimental, 253=Restricted,
    /// 254=HighlyRestricted, 255=Disabled.
    /// </summary>
    public static TcpAutotuningLevel MapAutotuningLevel(int value)
    {
        return value switch
        {
            0 => TcpAutotuningLevel.Normal,
            252 => TcpAutotuningLevel.Experimental,
            253 => TcpAutotuningLevel.Restricted,
            254 => TcpAutotuningLevel.HighlyRestricted,
            255 => TcpAutotuningLevel.Disabled,
            _ => TcpAutotuningLevel.Unknown
        };
    }

    private static int? ReadDword(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName);
            return value switch
            {
                int i => i,
                uint u => (int)u,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Read a value from every interface subkey and return the largest, so a per-interface
    /// tweak (e.g. TcpWindowSize) is still surfaced even when interfaces differ.
    /// </summary>
    private static int? ReadMaxInterfaceDword(string valueName)
    {
        int? max = null;
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(ParametersKey + @"\Interfaces");
            if (root is null)
                return null;
            foreach (var subName in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(subName);
                var value = sub?.GetValue(valueName);
                var dword = value switch
                {
                    int i => i,
                    uint u => (int)u,
                    _ => (int?)null
                };
                if (dword is int d && (max is null || d > max))
                    max = d;
            }
        }
        catch
        {
            return null;
        }
        return max;
    }
}