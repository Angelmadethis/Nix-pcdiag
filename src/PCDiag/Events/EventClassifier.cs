using PCDiag.Core;

namespace PCDiag.Events;

/// <summary>
/// Pure classifier that maps an event log record to a diagnostic category, a base
/// severity, and a friendly component name. The mapping is evidence-based (provider
/// name + event ID) and deliberately conservative: only well-known providers and
/// event IDs are classified; everything else returns null and is ignored by
/// aggregation. No message text is parsed, so classification never depends on
/// localized strings.
/// </summary>
public static class EventClassifier
{
    /// <summary>Classify a record, or null when it does not match any inspected category.</summary>
    public static ClassifiedEvent? Classify(EventLogRecord record)
    {
        var provider = record.Provider ?? string.Empty;
        var id = record.EventId;

        if (IsProvider(provider, "Microsoft-Windows-WHEA-Logger"))
            return Classified(record, EventCategory.Whea, WheaSeverity(id), "WHEA (Windows Hardware Error Architecture)");

        if (IsProvider(provider, "disk") && DiskIds.Contains(id))
            return Classified(record, EventCategory.Disk, DiskSeverity(id), "Disk subsystem");

        if (IsProvider(provider, "Ntfs") && NtfsIds.Contains(id))
            return Classified(record, EventCategory.Ntfs, DiagnosticSeverity.Warning, "NTFS filesystem");

        if (IsStorageController(provider) && (IsAtMostWarning(record) || StorageKnownIds.Contains(id)))
            return Classified(record, EventCategory.StorageController, DiagnosticSeverity.Warning, StorageControllerComponent(provider));

        if (IsDisplayGpu(provider) && (IsAtMostWarning(record) || DisplayGpuKnownIds.Contains(id)))
            return Classified(record, EventCategory.DisplayGpu, DiagnosticSeverity.Suspicious, DisplayComponent(provider));

        if (IsProvider(provider, "Microsoft-Windows-Kernel-Power") && KernelPowerIds.Contains(id))
            return Classified(record, EventCategory.KernelPower, KernelPowerSeverity(id), "Kernel-Power");

        if (IsNetworkAdapter(provider) && IsAtMostWarning(record))
            return Classified(record, EventCategory.NetworkAdapter, DiagnosticSeverity.Suspicious, NetworkComponent(provider));

        if (IsUsb(provider) && (IsAtMostWarning(record) || UsbResetIds.Contains(id)))
            return Classified(record, EventCategory.Usb, DiagnosticSeverity.Suspicious, "USB host controller");

        if (IsProvider(provider, "Service Control Manager"))
        {
            if (id == 7026)
                return Classified(record, EventCategory.DriverFailure, DiagnosticSeverity.Warning, "Service Control Manager (driver load)");
            if (ServiceFailureIds.Contains(id))
                return Classified(record, EventCategory.ServiceFailure, ServiceFailureSeverity(id), "Service Control Manager");
            return null;
        }

        if (IsProvider(provider, "BugCheck"))
            return Classified(record, EventCategory.DriverFailure, DiagnosticSeverity.Critical, "System bugcheck");

        if (IsProvider(provider, "Microsoft-Windows-WER-SystemErrorReporting") && id == 1001)
            return Classified(record, EventCategory.DriverFailure, DiagnosticSeverity.Critical, "System error reporting (bugcheck)");

        if (IsProvider(provider, "Microsoft-Windows-CodeIntegrity") && CodeIntegrityIds.Contains(id))
            return Classified(record, EventCategory.DriverFailure, DiagnosticSeverity.Warning, "Code Integrity");

        if (IsProvider(provider, "Microsoft-Windows-DriverFrameworks-UserMode") && UserModeDriverIds.Contains(id))
            return Classified(record, EventCategory.DriverFailure, DiagnosticSeverity.Warning, "User-mode driver framework");

        return null;
    }

    private static ClassifiedEvent Classified(EventLogRecord record, EventCategory category, DiagnosticSeverity severity, string component)
        => new()
        {
            Record = record,
            Category = category,
            Severity = severity,
            Component = component
        };

    private static bool IsProvider(string provider, string expected)
        => string.Equals(provider, expected, StringComparison.OrdinalIgnoreCase);

    private static bool AnyId(int id, params int[] ids)
        => ids.Contains(id);

    private static DiagnosticSeverity WheaSeverity(int id)
        => AnyId(id, 1, 20) ? DiagnosticSeverity.Critical
           : AnyId(id, 18, 19, 41, 47) ? DiagnosticSeverity.Suspicious
           : DiagnosticSeverity.Warning;

    private static DiagnosticSeverity DiskSeverity(int id)
        => id == 51 ? DiagnosticSeverity.Critical
           : id == 52 ? DiagnosticSeverity.Info
           : DiagnosticSeverity.Warning;

    private static DiagnosticSeverity KernelPowerSeverity(int id)
        => id == 41 ? DiagnosticSeverity.Critical
           : DiagnosticSeverity.Warning;

    private static DiagnosticSeverity ServiceFailureSeverity(int id)
        => AnyId(id, 7036, 7040) ? DiagnosticSeverity.Info
           : DiagnosticSeverity.Warning;

    private static readonly HashSet<int> ServiceFailureIds = new() { 7000, 7001, 7009, 7011, 7031, 7032, 7034, 7036, 7040 };
    private static readonly HashSet<int> CodeIntegrityIds = new() { 3004, 3023, 3033, 3040 };
    private static readonly HashSet<int> UserModeDriverIds = new() { 120, 219 };

    private static readonly HashSet<int> DiskIds = new() { 7, 11, 51, 52, 55, 153, 157 };
    private static readonly HashSet<int> NtfsIds = new() { 55, 57, 98, 130, 131, 132, 133 };
    private static readonly HashSet<int> KernelPowerIds = new() { 41, 137 };
    private static readonly HashSet<int> StorageKnownIds = new() { 11, 51, 153, 157, 219 };
    private static readonly HashSet<int> DisplayGpuKnownIds = new() { 4101, 4098, 4099, 4102 };
    private static readonly HashSet<int> UsbResetIds = new() { 219, 220, 230, 231, 400, 600, 700, 701, 702, 703 };

    /// <summary>True when the record level is Warning (3) or worse (Error/Critical). Logs that
    /// carry mostly informational events are only classified at Warning level or worse.</summary>
    private static bool IsAtMostWarning(EventLogRecord record)
        => record.Level is byte level && level <= 3;

    private static readonly string[] StorageControllerProviders =
    {
        "Microsoft-Windows-StorAHCI", "Microsoft-Windows-StorNVMe", "Microsoft-Windows-StorPort",
        "storahci", "stornvme", "StorPort", "iaStor", "iaStorAV", "iaStorV"
    };

    private static readonly string[] DisplayGpuProviders =
    {
        "Display", "nvlddmkm", "amdkmdag", "atikmdag", "amdkgd", "igfx", "igfxcui",
        "igfxCUIService2.0.0.0", "Intel Graphics", "BasicDisplay", "BasicRender",
        "display"
    };

    private static readonly string[] UsbProviders =
    {
        "Microsoft-Windows-USB-USBHUB3", "Microsoft-Windows-USB-USBXHCI",
        "Microsoft-Windows-USB-UCX", "Microsoft-Windows-USB-USBHUB2",
        "Microsoft-Windows-USB-USBPORT", "usb", "WinUSB"
    };

    private static bool IsStorageController(string provider)
        => StorageControllerProviders.Any(p => IsProvider(provider, p));

    private static bool IsDisplayGpu(string provider)
        => DisplayGpuProviders.Any(p => IsProvider(provider, p));

    private static bool IsUsb(string provider)
        => UsbProviders.Any(p => IsProvider(provider, p));

    private static bool IsNetworkAdapter(string provider)
    {
        if (IsGenericNetworkProvider(provider))
            return true;

        foreach (var prefix in NetworkPrefixes)
        {
            if (provider.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Generic Windows networking providers that log many informational events;
    /// these are classified only when the record is Warning level or worse.</summary>
    private static bool IsGenericNetworkProvider(string provider)
        => IsProvider(provider, "ndis") || IsProvider(provider, "Microsoft-Windows-NDIS")
           || IsProvider(provider, "Microsoft-Windows-NetworkProfile")
           || IsProvider(provider, "Microsoft-Windows-NetworkLocationWizard");

    private static readonly string[] NetworkPrefixes =
    {
        "e1d", "e1i", "e1k", "Netwtw", "rtwl", "athw", "bcmwl", "iwlwifi",
        "Killer", "Qualcomm", "Broadcom", "vmxnet", "vmnet", "Microsoft-Windows-NDIS"
    };

    private static string StorageControllerComponent(string provider)
        => provider switch
        {
            "Microsoft-Windows-StorAHCI" or "storahci" => "AHCI storage controller",
            "Microsoft-Windows-StorNVMe" or "stornvme" => "NVMe storage controller",
            "Microsoft-Windows-StorPort" or "StorPort" => "Storage port driver",
            "iaStor" or "iaStorAV" or "iaStorV" => "Intel storage controller",
            _ => provider
        };

    private static string DisplayComponent(string provider)
        => provider switch
        {
            "Display" or "display" => "Windows Display driver",
            "nvlddmkm" => "NVIDIA display driver",
            "amdkmdag" or "atikmdag" or "amdkgd" => "AMD display driver",
            "igfx" or "igfxcui" or "igfxCUIService2.0.0.0" or "Intel Graphics" => "Intel graphics driver",
            "BasicDisplay" => "Basic display driver",
            "BasicRender" => "Basic render driver",
            _ => provider
        };

    private static string NetworkComponent(string provider)
    {
        if (IsProvider(provider, "ndis") || IsProvider(provider, "Microsoft-Windows-NDIS"))
            return "NDIS";
        if (IsProvider(provider, "Microsoft-Windows-NetworkProfile"))
            return "Network profile";
        if (provider.StartsWith("Netwtw", StringComparison.OrdinalIgnoreCase))
            return "Intel Wi-Fi driver";
        if (provider.StartsWith("e1d", StringComparison.OrdinalIgnoreCase)
            || provider.StartsWith("e1i", StringComparison.OrdinalIgnoreCase)
            || provider.StartsWith("e1k", StringComparison.OrdinalIgnoreCase))
            return "Intel Ethernet driver";
        if (provider.StartsWith("rtwl", StringComparison.OrdinalIgnoreCase))
            return "Realtek Wi-Fi driver";
        if (provider.StartsWith("athw", StringComparison.OrdinalIgnoreCase))
            return "Qualcomm/Atheros Wi-Fi driver";
        if (provider.StartsWith("bcmwl", StringComparison.OrdinalIgnoreCase)
            || provider.StartsWith("Broadcom", StringComparison.OrdinalIgnoreCase))
            return "Broadcom networking driver";
        if (provider.StartsWith("Killer", StringComparison.OrdinalIgnoreCase))
            return "Killer networking driver";
        if (provider.StartsWith("Qualcomm", StringComparison.OrdinalIgnoreCase))
            return "Qualcomm networking driver";
        if (provider.StartsWith("iwlwifi", StringComparison.OrdinalIgnoreCase))
            return "Intel Wi-Fi driver";
        if (provider.StartsWith("vmxnet", StringComparison.OrdinalIgnoreCase)
            || provider.StartsWith("vmnet", StringComparison.OrdinalIgnoreCase))
            return "VMware networking driver";
        return provider;
    }
}