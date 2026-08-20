namespace PCDiag.Events;

/// <summary>
/// Tuning constants for the event log engine: observation window, read bounds,
/// the channels/providers/IDs inspected, and the repetition thresholds that drive
/// pattern detection. All thresholds are documented in SPEC.md Phase 7.
/// </summary>
public sealed record EventLogOptions
{
    /// <summary>How far back to inspect (14 days of recent history).</summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromDays(14);

    /// <summary>Maximum events read from any single channel before stopping.</summary>
    public int MaxEventsPerChannel { get; init; } = 2000;

    /// <summary>The channels inspected, with their provider/ID filters.</summary>
    public IReadOnlyList<EventChannelFilter> Channels { get; init; } = BuildDefaultChannels();

    // Repetition thresholds (counts over the window). "Suspicious" = unusual pattern;
    // "Warning" = the pattern is strong enough to likely impact stability.

    /// <summary>Corrected WHEA errors: &gt;= this many is a pattern.</summary>
    public int WheaSuspiciousCount { get; init; } = 2;

    /// <summary>Corrected WHEA errors: &gt;= this many is a strong pattern.</summary>
    public int WheaWarningCount { get; init; } = 6;

    /// <summary>Disk errors: pattern threshold.</summary>
    public int DiskSuspiciousCount { get; init; } = 2;

    /// <summary>Disk errors: strong-pattern threshold.</summary>
    public int DiskWarningCount { get; init; } = 5;

    /// <summary>NTFS errors: pattern threshold.</summary>
    public int NtfsSuspiciousCount { get; init; } = 2;

    /// <summary>NTFS errors: strong-pattern threshold.</summary>
    public int NtfsWarningCount { get; init; } = 5;

    /// <summary>Storage controller errors: pattern threshold.</summary>
    public int StorageSuspiciousCount { get; init; } = 2;

    /// <summary>Storage controller errors: strong-pattern threshold.</summary>
    public int StorageWarningCount { get; init; } = 5;

    /// <summary>GPU driver resets (TDR): pattern threshold.</summary>
    public int GpuSuspiciousCount { get; init; } = 2;

    /// <summary>GPU driver resets (TDR): strong-pattern threshold.</summary>
    public int GpuWarningCount { get; init; } = 5;

    /// <summary>Kernel-Power events: pattern threshold.</summary>
    public int KernelPowerSuspiciousCount { get; init; } = 2;

    /// <summary>Kernel-Power events: strong-pattern threshold.</summary>
    public int KernelPowerWarningCount { get; init; } = 5;

    /// <summary>Network adapter errors: pattern threshold. NICs (especially Wi-Fi) log
    /// occasional link/driver errors during normal use, so this starts higher than other
    /// categories.</summary>
    public int NetworkSuspiciousCount { get; init; } = 5;

    /// <summary>Network adapter errors: strong-pattern threshold.</summary>
    public int NetworkWarningCount { get; init; } = 15;

    /// <summary>USB resets: pattern threshold.</summary>
    public int UsbSuspiciousCount { get; init; } = 3;

    /// <summary>USB resets: strong-pattern threshold.</summary>
    public int UsbWarningCount { get; init; } = 8;

    /// <summary>Service failures: pattern threshold.</summary>
    public int ServiceSuspiciousCount { get; init; } = 3;

    /// <summary>Service failures: strong-pattern threshold.</summary>
    public int ServiceWarningCount { get; init; } = 8;

    /// <summary>Driver failures (load/signature): pattern threshold.</summary>
    public int DriverSuspiciousCount { get; init; } = 2;

    /// <summary>Driver failures (load/signature): strong-pattern threshold.</summary>
    public int DriverWarningCount { get; init; } = 5;

    public static EventLogOptions Default => new();

    /// <summary>Build the default channel set with provider/ID filters.</summary>
    public static IReadOnlyList<EventChannelFilter> BuildDefaultChannels()
    {
        return new List<EventChannelFilter>
        {
            new()
            {
                Channel = "System",
                Providers = SystemProviders,
                EventIds = Array.Empty<int>()
            },
            new() { Channel = "Microsoft-Windows-WHEA-Logger/Operational" },
            new() { Channel = "Microsoft-Windows-StorPort/Operational" },
            new() { Channel = "Microsoft-Windows-USB-USBHUB3/Operational" },
            new() { Channel = "Microsoft-Windows-USB-USBXHCI/Operational" },
            new() { Channel = "Microsoft-Windows-DriverFrameworks-UserMode/Operational" }
        };
    }

    /// <summary>
    /// Providers inspected in the System channel. The classifier is the authority on
    /// meaning; this list is a native query filter so the OS skips unrelated events.
    /// Unlisted providers are a documented limitation (see SPEC.md Phase 7).
    /// </summary>
    private static readonly string[] SystemProviders =
    {
        // WHEA
        "Microsoft-Windows-WHEA-Logger",
        // Storage / filesystem
        "disk", "Ntfs", "Microsoft-Windows-StorAHCI", "Microsoft-Windows-StorNVMe",
        "Microsoft-Windows-StorPort", "storahci", "stornvme", "StorPort",
        "iaStor", "iaStorAV", "iaStorV",
        // Display / GPU driver
        "Display", "nvlddmkm", "amdkmdag", "atikmdag", "amdkgd", "igfx", "igfxcui",
        "igfxCUIService2.0.0.0", "Intel Graphics", "BasicDisplay", "BasicRender",
        // Power
        "Microsoft-Windows-Kernel-Power",
        // Network adapter (common NIC sources)
        "ndis", "Microsoft-Windows-NDIS", "Microsoft-Windows-NetworkProfile",
        "e1dexpress", "e1i65x64", "e1kexpress", "Netwtw6", "Netwtw08", "Netwtw04",
        "Netwtw10", "Netwtw12", "Netwtw14", "Netwtw16", "rtwlane", "rtwlanu", "athw8x", "athwnx", "bcmwl",
        "Killer Network Service", "vmnetadapter",
        // USB
        "Microsoft-Windows-USB-USBHUB3", "Microsoft-Windows-USB-USBXHCI",
        "Microsoft-Windows-USB-UCX", "Microsoft-Windows-USB-USBHUB2",
        "Microsoft-Windows-USB-USBPORT", "usb", "WinUSB",
        // Services / drivers
        "Service Control Manager", "BugCheck", "Microsoft-Windows-WER-SystemErrorReporting",
        "Microsoft-Windows-CodeIntegrity", "Microsoft-Windows-DriverFrameworks-UserMode"
    };
}