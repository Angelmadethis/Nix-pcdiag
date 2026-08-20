namespace PCDiag.Events;

/// <summary>
/// The diagnostic categories the event log engine inspects. <see cref="None"/> is
/// returned by the classifier for events that fall outside every known category;
/// those events are ignored by aggregation and never surfaced in reports.
/// </summary>
public enum EventCategory
{
    /// <summary>Event did not match any inspected category.</summary>
    None,

    /// <summary>WHEA hardware errors (Machine Check Exceptions, corrected/fatal records).</summary>
    Whea,

    /// <summary>Disk subsystem errors reported by the disk/storage driver.</summary>
    Disk,

    /// <summary>NTFS filesystem errors.</summary>
    Ntfs,

    /// <summary>Storage controller driver errors (storahci / stornvme / StorPort / iaStor).</summary>
    StorageController,

    /// <summary>Display / GPU driver events, including TDR (driver stopped responding and recovered).</summary>
    DisplayGpu,

    /// <summary>Kernel-Power events (unexpected shutdown, firmware, critical state).</summary>
    KernelPower,

    /// <summary>Network adapter resets/errors reported by NIC vendors or Windows networking.</summary>
    NetworkAdapter,

    /// <summary>USB host controller / hub resets and errors.</summary>
    Usb,

    /// <summary>Windows services failing to start or terminating unexpectedly.</summary>
    ServiceFailure,

    /// <summary>Driver failures: bugchecks, driver load/signature failures.</summary>
    DriverFailure
}