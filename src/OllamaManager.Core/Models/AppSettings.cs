namespace OllamaManager.Core.Models;

public class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    public string ApiHost { get; set; } = "127.0.0.1";
    public int ApiPort { get; set; } = 11434;
    public string? CustomOllamaExecutable { get; set; }
    public string? CustomModelsDirectory { get; set; }

    public string Language { get; set; } = "zh-CN";
    public string Theme { get; set; } = "System";

    public int RefreshIntervalOllamaSeconds { get; set; } = 5;
    public int RefreshIntervalRunningSeconds { get; set; } = 2;
    public int RefreshIntervalGpuSeconds { get; set; } = 2;
    public int RefreshIntervalModelsSeconds { get; set; } = 15;

    public int HttpTimeoutSeconds { get; set; } = 30;
    public int StartupTimeoutSeconds { get; set; } = 30;
    public int ShutdownTimeoutSeconds { get; set; } = 10;

    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;

    public string LogLevel { get; set; } = "Information";
    public int MaxLogFileSizeMB { get; set; } = 10;
    public int MaxLogFiles { get; set; } = 10;

    public bool ConfirmDangerousOperations { get; set; } = true;
    public bool ForceKillConfirmation { get; set; } = true;
    public bool EnableTelemetry { get; set; } = false;
    public bool UseNvidiaGpuMonitoring { get; set; } = true;
    public bool AllowDeleteModels { get; set; } = true;
    public bool AllowReleaseAllModels { get; set; } = true;

    public bool PortableMode { get; set; } = false;

    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 800;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public bool WindowMaximized { get; set; } = false;

    public string OllamaStartupArgs { get; set; } = string.Empty;
}

public enum AppTheme
{
    Light,
    Dark,
    System
}
