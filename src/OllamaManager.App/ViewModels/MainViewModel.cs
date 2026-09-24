using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class MainViewModel : AsyncObservableObject, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOllamaService _ollamaService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly DispatcherTimer _statusTimer;

    private ObservableObject? _currentPage;
    public ObservableObject? CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private DateTime _lastUpdated = DateTime.Now;
    public DateTime LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    private string _ollamaStatusKind = "Unknown";
    public string OllamaStatusKind
    {
        get => _ollamaStatusKind;
        set
        {
            if (SetProperty(ref _ollamaStatusKind, value))
            {
                OllamaStatusText = value switch
                {
                    "Running" => L("Status.Running"),
                    "NotRunning" => L("Status.NotRunning"),
                    "NotInstalled" => L("Status.NotInstalled"),
                    _ => L("Status.Unknown")
                };
            }
        }
    }

    private string _ollamaStatusText = "未知";
    public string OllamaStatusText
    {
        get => _ollamaStatusText;
        private set => SetProperty(ref _ollamaStatusText, value);
    }

    private NavItem? _selectedNavItem;
    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            if (SetProperty(ref _selectedNavItem, value))
            {
                foreach (var item in NavItems) item.IsSelected = ReferenceEquals(item, value);
            }
        }
    }

    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>Models currently loaded in Ollama. Polled every 2 s.</summary>
    public ObservableCollection<RunningModelInfo> RunningModels { get; } = new();

    public int RunningModelCount => RunningModels.Count;

    private readonly Dictionary<string, Func<ObservableObject>> _pageFactories = new();
    private readonly Dictionary<string, ObservableObject> _pageCache = new();

    public MainViewModel(
        IServiceProvider serviceProvider,
        IOllamaService ollamaService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _serviceProvider = serviceProvider;
        _ollamaService = ollamaService;
        _localizationService = localizationService;
        _logService = logService;

        _statusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _statusTimer.Tick += async (_, _) =>
        {
            try { await PollRunningStatusAsync(); }
            catch (Exception ex) { _logService.Warn("Main", $"Polling error: {ex.Message}"); }
        };

        _pageFactories["Dashboard"] = () => _serviceProvider.GetRequiredService<DashboardViewModel>();
        _pageFactories["Models"] = () =>
        {
            var vm = _serviceProvider.GetRequiredService<ModelsViewModel>();
            vm.ModelLoaded -= OnModelLoaded;
            vm.ModelLoaded += OnModelLoaded;
            return vm;
        };
        _pageFactories["Running"] = () => _serviceProvider.GetRequiredService<RunningViewModel>();
        _pageFactories["Chat"] = () => _serviceProvider.GetRequiredService<ChatViewModel>();
        _pageFactories["Modelfile"] = () => _serviceProvider.GetRequiredService<ModelfileViewModel>();
        _pageFactories["Benchmark"] = () => _serviceProvider.GetRequiredService<BenchmarkViewModel>();
        _pageFactories["GPU"] = () => _serviceProvider.GetRequiredService<GpuViewModel>();
        _pageFactories["Diagnostics"] = () => _serviceProvider.GetRequiredService<DiagnosticsViewModel>();
        _pageFactories["Backup"] = () => _serviceProvider.GetRequiredService<BackupViewModel>();
        _pageFactories["Logs"] = () => _serviceProvider.GetRequiredService<LogsViewModel>();
        _pageFactories["Settings"] = () => _serviceProvider.GetRequiredService<SettingsViewModel>();

        _localizationService.LanguageChanged += OnLanguageChanged;
        InitNavItems();
    }

    public void Dispose()
    {
        _statusTimer.Stop();
    }

    private async Task PollRunningStatusAsync()
    {
        if (CurrentPage is ModelsViewModel modelsVm)
        {
            await modelsVm.RefreshStatusAsync();
        }
        else if (CurrentPage is RunningViewModel runningVm)
        {
            await runningVm.LoadRunningModelsAsync();
        }

        // Always keep the running-model list fresh so the tray menu's "运载模型" section is live.
        try
        {
            var models = await _ollamaService.GetRunningModelsAsync();
            SyncRunningModels(models);
        }
        catch (Exception ex)
        {
            _logService.Debug("Main", $"Running models poll failed: {ex.Message}");
        }
    }

    private void SyncRunningModels(IList<RunningModelInfo> latest)
    {
        // Remove models no longer present
        for (int i = RunningModels.Count - 1; i >= 0; i--)
        {
            var existing = RunningModels[i];
            if (!latest.Any(m => string.Equals(m.Name, existing.Name, StringComparison.OrdinalIgnoreCase)))
                RunningModels.RemoveAt(i);
        }
        // Add or update
        foreach (var m in latest)
        {
            var existing = RunningModels.FirstOrDefault(r => string.Equals(r.Name, m.Name, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                RunningModels.Add(m);
            else
            {
                existing.Size = m.Size;
                existing.SizeVram = m.SizeVram;
                existing.ExpiresAt = m.ExpiresAt;
                existing.Model = m.Model;
            }
        }
    }

    private void OnModelLoaded(string modelName)
    {
        Application.Current?.Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(150);
            NavigateTo("Chat");
            if (CurrentPage is ChatViewModel chat)
            {
                chat.SetModel(modelName);
            }
        });
    }

    private void OnLanguageChanged(object? sender, CultureInfo e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ApplyLanguage(e));
        }
        else
        {
            ApplyLanguage(e);
        }
    }

    private static readonly Dictionary<string, string> NavKeyToLocKey = new(StringComparer.Ordinal)
    {
        ["Dashboard"] = "Nav.Dashboard",
        ["Models"] = "Nav.Models",
        ["Running"] = "Nav.Running",
        ["Chat"] = "Nav.Chat",
        ["Modelfile"] = "Nav.Modelfile",
        ["Benchmark"] = "Nav.Benchmark",
        ["GPU"] = "Nav.Gpu",
        ["Diagnostics"] = "Nav.Diagnostics",
        ["Backup"] = "Nav.Backup",
        ["Logs"] = "Nav.Logs",
        ["Settings"] = "Nav.Settings"
    };

    private void ApplyLanguage(CultureInfo e)
    {
        foreach (var item in NavItems)
        {
            item.Label = NavKeyToLocKey.TryGetValue(item.Key, out var locKey) ? L(locKey) : item.Key;
        }
        if (_lastOllamaStatus != null)
        {
            OllamaStatusKind = MapOllamaStatus(_lastOllamaStatus);
        }
        OnPropertyChanged(nameof(StatusMessage));
        StatusMessage = L("App.Ready");
    }

    private void InitNavItems()
    {
        NavItems.Add(new NavItem { Key = "Dashboard", Label = L(NavKeyToLocKey["Dashboard"]), Glyph = "⌂" });
        NavItems.Add(new NavItem { Key = "Models", Label = L(NavKeyToLocKey["Models"]), Glyph = "⊞" });
        NavItems.Add(new NavItem { Key = "Running", Label = L(NavKeyToLocKey["Running"]), Glyph = "▶" });
        NavItems.Add(new NavItem { Key = "Chat", Label = L(NavKeyToLocKey["Chat"]), Glyph = "💬" });
        NavItems.Add(new NavItem { Key = "Modelfile", Label = L(NavKeyToLocKey["Modelfile"]), Glyph = "⚙" });
        NavItems.Add(new NavItem { Key = "Benchmark", Label = L(NavKeyToLocKey["Benchmark"]), Glyph = "⌖" });
        NavItems.Add(new NavItem { Key = "GPU", Label = L(NavKeyToLocKey["GPU"]), Glyph = "⚡" });
        NavItems.Add(new NavItem { Key = "Diagnostics", Label = L(NavKeyToLocKey["Diagnostics"]), Glyph = "🔍" });
        NavItems.Add(new NavItem { Key = "Backup", Label = L(NavKeyToLocKey["Backup"]), Glyph = "💾" });
        NavItems.Add(new NavItem { Key = "Logs", Label = L(NavKeyToLocKey["Logs"]), Glyph = "📜" });
        NavItems.Add(new NavItem { Key = "Settings", Label = L(NavKeyToLocKey["Settings"]), Glyph = "🔧" });
    }

    private string L(string key) => _localizationService[key];

    public async Task InitializeAsync()
    {
        StatusMessage = L("App.Initializing");
        _statusTimer.Start();
        try
        {
            var status = await _ollamaService.GetCurrentStatusAsync();
            UpdateOllamaStatus(status);
            // Prime the running-model list once so the tray menu has data on first open
            try
            {
                var models = await _ollamaService.GetRunningModelsAsync();
                SyncRunningModels(models);
            }
            catch (Exception ex) { _logService.Debug("Main", $"Initial running models fetch failed: {ex.Message}"); }
            StatusMessage = L("App.Ready");
        }
        catch (Exception ex)
        {
            _logService.Error("App", $"Initialization error: {ex.Message}", ex);
            StatusMessage = L("App.InitFailed");
            OllamaStatusKind = "Error";
        }
    }

    public async void NavigateTo(string key)
    {
        if (!_pageFactories.ContainsKey(key)) return;

        StatusMessage = $"{L("App.Loading")} {key}...";
        try
        {
            if (!_pageCache.TryGetValue(key, out var page))
            {
                page = _pageFactories[key]();
                _pageCache[key] = page;
            }

            CurrentPage = page;
            SelectedNavItem = NavItems.FirstOrDefault(n => n.Key == key);
            LastUpdated = DateTime.Now;

            if (page is DashboardViewModel dashboard)
            {
                await dashboard.LoadAsync();
            }
            else if (page is ModelsViewModel models)
            {
                await models.InitializeAsync();
            }
            else if (page is RunningViewModel running)
            {
                await running.InitializeAsync();
            }
            else if (page is ChatViewModel chat)
            {
                await chat.InitializeAsync();
            }
            else if (page is GpuViewModel gpu)
            {
                await gpu.LoadGpuInfoAsync();
            }
            else if (page is DiagnosticsViewModel diag)
            {
                await diag.InitializeAsync();
            }
            else if (page is LogsViewModel logs)
            {
                await logs.InitializeAsync();
            }
            else if (page is SettingsViewModel settings)
            {
                await settings.InitializeAsync();
            }
            else if (page is BackupViewModel backup)
            {
                await backup.InitializeAsync();
            }
            else if (page is BenchmarkViewModel bench)
            {
                await bench.InitializeAsync();
            }

            StatusMessage = L("App.Ready");
        }
        catch (Exception ex)
        {
            _logService.Error("Nav", $"Navigation error: {ex.Message}", ex);
            StatusMessage = L("App.NavFailed");
        }
    }

    private OllamaStatus? _lastOllamaStatus;

    private void UpdateOllamaStatus(OllamaStatus status)
    {
        _lastOllamaStatus = status;
        OllamaStatusKind = MapOllamaStatus(status);
    }

    private string MapOllamaStatus(OllamaStatus status)
    {
        // Note: fully qualify the enum name to avoid colliding with the property of the same name
        return status.Kind switch
        {
            Core.Models.OllamaStatusKind.ApiAvailable => "Running",
            Core.Models.OllamaStatusKind.RunningButApiUnavailable => "NotRunning",
            Core.Models.OllamaStatusKind.InstalledButNotRunning => "NotRunning",
            Core.Models.OllamaStatusKind.NotInstalled => "NotInstalled",
            _ => "Unknown"
        };
    }

    public void UpdateStatus(string message)
    {
        StatusMessage = message;
        LastUpdated = DateTime.Now;
    }
}
