using OllamaManager.Core.Models;

namespace OllamaManager.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_AreSane()
    {
        var s = new AppSettings();
        Assert.Equal("127.0.0.1", s.ApiHost);
        Assert.Equal(11434, s.ApiPort);
        Assert.False(s.EnableTelemetry);
        Assert.True(s.UseNvidiaGpuMonitoring);
        Assert.True(s.MinimizeToTrayOnClose);
    }

    [Fact]
    public void WindowLeft_DefaultsToNaN()
    {
        var s = new AppSettings();
        Assert.True(double.IsNaN(s.WindowLeft));
    }

    [Fact]
    public void WindowSize_Defaults()
    {
        var s = new AppSettings();
        Assert.Equal(1280, s.WindowWidth);
        Assert.Equal(800, s.WindowHeight);
    }
}