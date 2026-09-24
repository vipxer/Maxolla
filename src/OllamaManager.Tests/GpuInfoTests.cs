using OllamaManager.Core.Models;

namespace OllamaManager.Tests;

public class GpuInfoTests
{
    [Fact]
    public void MemoryFree_SubtractsUsedFromTotal()
    {
        var gpu = new GpuInfo { MemoryTotal = 1000, MemoryUsed = 300 };
        Assert.Equal(700, gpu.MemoryFree);
    }

    [Fact]
    public void MemoryFree_NegativeClampsToZero()
    {
        var gpu = new GpuInfo { MemoryTotal = 100, MemoryUsed = 200 };
        Assert.Equal(0, gpu.MemoryFree);
    }

    [Fact]
    public void MemoryUtilizationPercent_CalculatesCorrectly()
    {
        var gpu = new GpuInfo { MemoryTotal = 1000, MemoryUsed = 250 };
        Assert.Equal(25.0, gpu.MemoryUtilizationPercent, 1);
    }

    [Fact]
    public void DisplayMemoryTotal_FormatsBytes()
    {
        var gpu = new GpuInfo { MemoryTotal = 1024 * 1024 };
        Assert.Equal("1 MB", gpu.DisplayMemoryTotal);
    }
}