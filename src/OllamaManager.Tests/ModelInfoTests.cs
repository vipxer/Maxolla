using OllamaManager.Core.Models;

namespace OllamaManager.Tests;

public class ModelInfoTests
{
    [Fact]
    public void FormatBytes_Zero_ReturnsZeroB()
    {
        Assert.Equal("0 B", ModelInfo.FormatBytes(0));
    }

    [Fact]
    public void FormatBytes_OneKilobyte_ReturnsCorrectUnit()
    {
        Assert.Equal("1 KB", ModelInfo.FormatBytes(1024));
    }

    [Fact]
    public void FormatBytes_OneMegabyte_ReturnsCorrectUnit()
    {
        Assert.Equal("1 MB", ModelInfo.FormatBytes(1024 * 1024));
    }

    [Fact]
    public void FormatBytes_OneGigabyte_ReturnsCorrectUnit()
    {
        Assert.Equal("1 GB", ModelInfo.FormatBytes(1024L * 1024L * 1024L));
    }

    [Fact]
    public void FormatBytes_Fractional_ReturnsTwoDecimal()
    {
        var result = ModelInfo.FormatBytes(1536); // 1.5 KB
        Assert.Equal("1.5 KB", result);
    }

    [Fact]
    public void DisplaySize_MirrorsFormatBytes()
    {
        var m = new ModelInfo { Size = 2048 };
        Assert.Equal(ModelInfo.FormatBytes(2048), m.DisplaySize);
    }

    [Fact]
    public void IsRunning_DefaultsFalse()
    {
        var m = new ModelInfo { Name = "test" };
        Assert.False(m.IsRunning);
    }
}