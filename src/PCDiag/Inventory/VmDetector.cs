namespace PCDiag.Inventory;

/// <summary>
/// Pure heuristics that detect whether a machine is a virtual machine.
/// Reads no system state itself; all inputs are passed in so the logic is testable.
/// </summary>
public static class VmDetector
{
    /// <summary>
    /// Decide whether the machine is a VM based on manufacturer/model/CPU strings and
    /// the WMI <c>HypervisorPresent</c> flag. Returns true when strong VM evidence exists,
    /// false when it clearly does not, and null when it is ambiguous.
    /// </summary>
    public static bool? Detect(
        string? manufacturer,
        string? model,
        string? cpuName,
        bool? hypervisorPresent)
    {
        var signals = 0;

        if (hypervisorPresent == true)
            signals += 2;

        if (ContainsAnyIgnoreCase(manufacturer, "VMware", "VirtualBox", "innotek", "QEMU", "Bochs", "Xen", "Parallels", "Microsoft Corporation", "Red Hat"))
            signals += 2;

        if (ContainsAnyIgnoreCase(model, "Virtual Machine", "VMware", "VirtualBox", "QEMU", "Bochs", "Xen", "Parallels", "Virtual System"))
            signals += 2;

        if (ContainsAnyIgnoreCase(cpuName, "Virtual CPU", "QEMU", "VMware", "vCPU", "KVM"))
            signals += 1;

        if (signals >= 2)
            return true;
        if (signals == 0)
            return false;
        return null;
    }

    private static bool ContainsAnyIgnoreCase(string? value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}