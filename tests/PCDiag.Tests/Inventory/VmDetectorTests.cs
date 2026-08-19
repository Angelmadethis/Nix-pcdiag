using PCDiag.Inventory;

namespace PCDiag.Tests.Inventory;

public class VmDetectorTests
{
    [Theory]
    [InlineData("VMware, Inc.", "VMware Virtual Platform", "Intel(R) Core(TM) i7", true)]
    [InlineData("Microsoft Corporation", "Virtual Machine", "Intel(R) Core(TM) i7", null)]
    [InlineData("innotek GmbH", "VirtualBox", "QEMU Virtual CPU", false)]
    public void Detect_StrongEvidence_ShouldReturnTrue(
        string manufacturer, string model, string cpuName, bool? hypervisorPresent)
    {
        var result = VmDetector.Detect(manufacturer, model, cpuName, hypervisorPresent);

        Assert.True(result);
    }

    [Fact]
    public void Detect_NoSignals_ShouldReturnFalse()
    {
        var result = VmDetector.Detect("Dell Inc.", "OptiPlex 7090", "Intel(R) Core(TM) i7-11700", false);

        Assert.False(result);
    }

    [Fact]
    public void Detect_WeakSignals_ShouldReturnNull()
    {
        var result = VmDetector.Detect("Dell Inc.", "OptiPlex 7090", "QEMU Virtual CPU", false);

        Assert.Null(result);
    }

    [Fact]
    public void Detect_NullInputs_ShouldNotThrow()
    {
        var result = VmDetector.Detect(null, null, null, null);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("", "", "", null)]
    public void Detect_EmptyInputs_ShouldReturnFalse(string? manufacturer, string? model, string? cpuName, bool? hypervisorPresent)
    {
        var result = VmDetector.Detect(manufacturer, model, cpuName, hypervisorPresent);

        Assert.False(result);
    }
}