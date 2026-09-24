using OllamaManager.Core.Models;

namespace OllamaManager.Tests;

public class BackupModelsTests
{
    [Fact]
    public void BackupInfo_DisplaySize()
    {
        var b = new BackupInfo { SizeBytes = 1024 * 1024 };
        Assert.Equal("1 MB", b.DisplaySize);
    }

    [Fact]
    public void BackupEntry_DisplaySize()
    {
        var e = new BackupEntry { SizeBytes = 2048 };
        Assert.Equal("2 KB", e.DisplaySize);
    }

    [Fact]
    public void BackupProgress_CalculatesPercent()
    {
        var p = new BackupProgress { BytesProcessed = 250, TotalBytes = 1000 };
        Assert.Equal(25.0, p.PercentComplete);
    }

    [Fact]
    public void BackupProgress_ZeroTotal_ReturnsZero()
    {
        var p = new BackupProgress { BytesProcessed = 250, TotalBytes = 0 };
        Assert.Equal(0, p.PercentComplete);
    }
}