using System.Collections.ObjectModel;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class DashboardViewModel : AsyncObservableObject
{
    private readonly IOllamaService _ollamaService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    private string? _ollamaVersion;
    public string? OllamaVersion
    {
        get => _ollamaVersion;
        set => SetProperty(ref _ollamaVersion, value);
    }

    private string? _ollamaEndpoint;
    public string? OllamaEndpoint
    {
        get => _ollamaEndpoint;
        set => SetProperty(ref _ollamaEndpoint, value);
    }

    private string? _ollamaExecutablePath;
    public string? OllamaExecutablePath
    {
        get => _ollamaExecutablePath;
        set => SetProperty(ref _ollamaExecutablePath, value);
    }

    private string? _modelsDirectory;
    public string? ModelsDirectory
    {
        get => _modelsDirectory;
        set => SetProperty(ref _modelsDirectory, value);
    }

    private string? _discoveryMethod;
    public string? DiscoveryMethod
    {
        get => _discoveryMethod;
        set => SetProperty(ref _discoveryMethod, value);
    }

    private int _modelCount;
    public int ModelCount
    {
        get => _modelCount;
        set => SetProperty(ref _modelCount, value);
    }

    private int _runningModelCount;
    public int RunningModelCount
    {
        get => _runningModelCount;
        set => SetProperty(ref _runningModelCount, value);
    }

    private long _totalDiskUsageBytes;
    public long TotalDiskUsageBytes
    {
        get => _totalDiskUsageBytes;
        set
        {
            if (SetProperty(ref _totalDiskUsageBytes, value))
            {
                RaisePropertyChanged(nameof(TotalDiskUsageDisplay));
            }
        }
    }
    public string TotalDiskUsageDisplay => FormatBytes(TotalDiskUsageBytes);

    private string _statusMessage = "Unknown";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<RunningModelInfo> RunningModels { get; } = new();

    public DashboardViewModel(IOllamaService ollamaService, ILocalizationService loc, ILogService log)
    {
        _ollamaService = ollamaService;
        _loc = loc;
        _log = log;
    }

    public async Task LoadAsync()
    {
        await RunBusyAsync(_loc["Dashboard.Loading"], async () =>
        {
            try
            {
                var status = await _ollamaService.GetCurrentStatusAsync();
                OllamaVersion = status.Version ?? "—";
                OllamaEndpoint = status.ApiEndpoint ?? "—";
                OllamaExecutablePath = status.ExecutablePath ?? "—";
                ModelsDirectory = status.ModelsDirectory ?? "—";
                DiscoveryMethod = status.DiscoveryMethod ?? "—";
                StatusMessage = status.Kind.ToString();

                if (status.IsApiAvailable)
                {
                    var models = await _ollamaService.GetModelsAsync();
                    ModelCount = models.Count;
                    TotalDiskUsageBytes = models.Sum(m => m.Size);

                    var running = await _ollamaService.GetRunningModelsAsync();
                    RunningModelCount = running.Count;
                    RunningModels.Clear();
                    foreach (var r in running) RunningModels.Add(r);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Dashboard", $"Dashboard load error: {ex.Message}", ex);
                StatusMessage = "Error";
            }
        });
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }
}