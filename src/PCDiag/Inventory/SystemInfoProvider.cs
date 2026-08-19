using System.Runtime.InteropServices;
using PCDiag.Infrastructure;

namespace PCDiag.Inventory;

/// <summary>
/// Collects basic operating-system-level information using .NET APIs and the
/// documented Windows <c>RtlGetVersion</c> API for the precise build/UBR.
/// </summary>
public static class SystemInfoProvider
{
    public static OsInfo Collect()
    {
        var (build, ubr) = GetWindowsBuild();

        return new OsInfo
        {
            MachineName = Safe(() => Environment.MachineName) ?? "",
            OSVersionString = Safe(() => Environment.OSVersion.VersionString) ?? "",
            WindowsBuild = build,
            Ubr = ubr,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            Is64Bit = Environment.Is64BitOperatingSystem,
            IsVirtualMachine = DetectVirtualMachine()
        };
    }

    private static bool? DetectVirtualMachine()
    {
        var rows = WmiQuery.Query("SELECT Manufacturer, Model, HypervisorPresent FROM Win32_ComputerSystem");
        var row = rows.FirstOrDefault();
        var cpuName = WmiQuery.GetString(WmiQuery.Query("SELECT Name FROM Win32_Processor").FirstOrDefault(), "Name");

        bool? hypervisorPresent = null;
        if (row is not null && WmiQuery.GetInt32(row, "HypervisorPresent") is int h)
            hypervisorPresent = h == 1;

        return VmDetector.Detect(
            row is null ? null : WmiQuery.GetString(row, "Manufacturer"),
            row is null ? null : WmiQuery.GetString(row, "Model"),
            cpuName,
            hypervisorPresent);
    }

    private static (int Build, int Ubr) GetWindowsBuild()
    {
        var osvi = new OSVERSIONINFOEXW { dwOSVersionInfoSize = (uint)Marshal.SizeOf<OSVERSIONINFOEXW>() };
        if (RtlGetVersion(ref osvi) == 0)
        {
            return ((int)osvi.dwBuildNumber, (int)osvi.dwRevision);
        }

        return (Environment.OSVersion.Version.Build, 0);
    }

    private static T? Safe<T>(Func<T> action) where T : class
    {
        try
        {
            return action();
        }
        catch
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OSVERSIONINFOEXW
    {
        public uint dwOSVersionInfoSize;
        public uint dwMajorVersion;
        public uint dwMinorVersion;
        public uint dwBuildNumber;
        public uint dwPlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szCSDVersion;
        public ushort wServicePackMajor;
        public ushort wServicePackMinor;
        public ushort wSuiteMask;
        public byte wProductType;
        public byte wReserved;
        public uint dwRevision;
    }

    [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
    private static extern int RtlGetVersion(ref OSVERSIONINFOEXW lpVersionInformation);
}