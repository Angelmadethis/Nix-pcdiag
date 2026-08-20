using PCDiag.Core;
using PCDiag.Events;

namespace PCDiag.Tests.Events;

public class EventClassifierTests
{
    private static ClassifiedEvent? Classify(string provider, int id, byte? level = 3)
        => EventClassifier.Classify(Ev.New(provider, id, level: level));

    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(41)]
    [InlineData(47)]
    public void CorrectedWhea_IsSuspicious(int id)
    {
        var classified = Classify("Microsoft-Windows-WHEA-Logger", id);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.Whea, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Suspicious, classified.Severity);
        Assert.Contains("WHEA", classified.Component);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void FatalWhea_IsCritical(int id)
    {
        var classified = Classify("Microsoft-Windows-WHEA-Logger", id);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.Whea, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Critical, classified.Severity);
    }

    [Fact]
    public void DiskControllerError_IsWarning()
    {
        var classified = Classify("disk", 11);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.Disk, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Warning, classified.Severity);
        Assert.Equal("Disk subsystem", classified.Component);
    }

    [Fact]
    public void DiskPagingError_IsCritical()
    {
        var classified = Classify("disk", 51);
        Assert.Equal(DiagnosticSeverity.Critical, classified!.Severity);
    }

    [Fact]
    public void DiskClearEvent_IsInfo()
    {
        var classified = Classify("disk", 52);
        Assert.Equal(DiagnosticSeverity.Info, classified!.Severity);
    }

    [Fact]
    public void NtfsError_IsWarning()
    {
        var classified = Classify("Ntfs", 55);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.Ntfs, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Warning, classified.Severity);
        Assert.Equal("NTFS filesystem", classified.Component);
    }

    [Fact]
    public void StorAhci_IsStorageController()
    {
        var classified = Classify("Microsoft-Windows-StorAHCI", 153);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.StorageController, classified!.Category);
        Assert.Equal("AHCI storage controller", classified.Component);
    }

    [Fact]
    public void StorNvme_IsStorageController()
    {
        var classified = Classify("Microsoft-Windows-StorNVMe", 157);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.StorageController, classified!.Category);
        Assert.Equal("NVMe storage controller", classified.Component);
    }

    [Fact]
    public void Iastor_IsStorageController()
    {
        var classified = Classify("iaStor", 153);
        Assert.Equal(EventCategory.StorageController, classified!.Category);
        Assert.Equal("Intel storage controller", classified.Component);
    }

    [Fact]
    public void DisplayTdr_IsDisplayGpuSuspicious()
    {
        var classified = Classify("Display", 4101);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.DisplayGpu, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Suspicious, classified.Severity);
        Assert.Equal("Windows Display driver", classified.Component);
    }

    [Fact]
    public void NvidiaTdr_IsDisplayGpu()
    {
        var classified = Classify("nvlddmkm", 4101);

        Assert.Equal(EventCategory.DisplayGpu, classified!.Category);
        Assert.Equal("NVIDIA display driver", classified.Component);
    }

    [Fact]
    public void AmdTdr_IsDisplayGpu()
    {
        var classified = Classify("amdkmdag", 4101);
        Assert.Equal("AMD display driver", classified.Component);
    }

    [Fact]
    public void IntelGfx_IsDisplayGpu()
    {
        var classified = Classify("igfx", 4101);
        Assert.Equal("Intel graphics driver", classified.Component);
    }

    [Fact]
    public void KernelPower41_IsCritical()
    {
        var classified = Classify("Microsoft-Windows-Kernel-Power", 41);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.KernelPower, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Critical, classified.Severity);
    }

    [Fact]
    public void KernelPower109_IsNotClassified()
    {
        var classified = Classify("Microsoft-Windows-Kernel-Power", 109);
        Assert.Null(classified);
    }

    [Fact]
    public void KernelPower137_IsWarning()
    {
        var classified = Classify("Microsoft-Windows-Kernel-Power", 137);
        Assert.Equal(EventCategory.KernelPower, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Warning, classified.Severity);
    }

    [Fact]
    public void IntelEthernet_IsNetworkAdapter()
    {
        var classified = Classify("e1dexpress", 27);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.NetworkAdapter, classified!.Category);
        Assert.Equal("Intel Ethernet driver", classified.Component);
    }

    [Fact]
    public void IntelWifi_IsNetworkAdapter()
    {
        var classified = Classify("Netwtw6", 5000);
        Assert.Equal(EventCategory.NetworkAdapter, classified!.Category);
        Assert.Equal("Intel Wi-Fi driver", classified.Component);
    }

    [Fact]
    public void IntelWifi_InformationEvent_IsNotClassified()
    {
        var classified = Classify("Netwtw10", 7036, level: 4);
        Assert.Null(classified);
    }

    [Fact]
    public void IntelWifi_WarningEvent_IsClassified()
    {
        var classified = Classify("Netwtw10", 6062, level: 3);
        Assert.Equal(EventCategory.NetworkAdapter, classified!.Category);
        Assert.Equal("Intel Wi-Fi driver", classified.Component);
    }

    [Fact]
    public void NdisInformationEvent_IsNotClassified()
    {
        var classified = Classify("ndis", 7003, level: 4);
        Assert.Null(classified);
    }

    [Fact]
    public void DisplayInformationEvent_IsNotClassified()
    {
        var classified = Classify("Display", 4004, level: 4);
        Assert.Null(classified);
    }

    [Fact]
    public void UsbHubReset_IsUsbSuspicious()
    {
        var classified = Classify("Microsoft-Windows-USB-USBHUB3", 219);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.Usb, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Suspicious, classified.Severity);
    }

    [Theory]
    [InlineData(7000)]
    [InlineData(7001)]
    [InlineData(7031)]
    [InlineData(7034)]
    public void ScmServiceFailures_AreWarning(int id)
    {
        var classified = Classify("Service Control Manager", id);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.ServiceFailure, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Warning, classified.Severity);
    }

    [Fact]
    public void ScmStateChange_IsInfo()
    {
        var classified = Classify("Service Control Manager", 7036);
        Assert.Equal(EventCategory.ServiceFailure, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Info, classified.Severity);
    }

    [Fact]
    public void ScmDriverLoadFailure_IsDriverFailure()
    {
        var classified = Classify("Service Control Manager", 7026);

        Assert.Equal(EventCategory.DriverFailure, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Warning, classified.Severity);
    }

    [Fact]
    public void BugCheck_IsCriticalDriverFailure()
    {
        var classified = Classify("BugCheck", 1001);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.DriverFailure, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Critical, classified.Severity);
    }

    [Fact]
    public void WerSystemErrorReporting1001_IsCritical()
    {
        var classified = Classify("Microsoft-Windows-WER-SystemErrorReporting", 1001);

        Assert.NotNull(classified);
        Assert.Equal(EventCategory.DriverFailure, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Critical, classified.Severity);
    }

    [Fact]
    public void CodeIntegrityDriverBlock_IsDriverFailure()
    {
        var classified = Classify("Microsoft-Windows-CodeIntegrity", 3023);
        Assert.Equal(EventCategory.DriverFailure, classified!.Category);
        Assert.Equal(DiagnosticSeverity.Warning, classified.Severity);
    }

    [Fact]
    public void UnknownProvider_IsNotClassified()
    {
        var classified = Classify("Application Error", 1000);
        Assert.Null(classified);
    }

    [Fact]
    public void UnknownIdFromKnownProvider_IsNotClassified()
    {
        var classified = Classify("disk", 1);
        Assert.Null(classified);
    }

    [Fact]
    public void ScmNewServiceEvent_IsNotClassified()
    {
        var classified = Classify("Service Control Manager", 7045);
        Assert.Null(classified);
    }

    [Fact]
    public void ProviderMatching_IsCaseInsensitive()
    {
        var classified = Classify("DISK", 11);
        Assert.Equal(EventCategory.Disk, classified!.Category);
    }
}