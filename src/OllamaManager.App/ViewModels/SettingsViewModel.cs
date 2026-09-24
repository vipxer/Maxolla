using System.Globalization;
using System.Reflection;
using System.Windows.Input;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class SettingsViewModel : AsyncObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public string AppVersion { get; }

    private AppSettings _settings = new();
    public AppSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public IReadOnlyList<LanguageInfo> Languages { get; }

    private LanguageInfo? _selectedLanguage;
    public LanguageInfo? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                if (value != null)
                {
                    Settings.Language = value.CultureName;
                    _loc.SetLanguage(value.CultureName);
                    _ = SaveAsync();
                }
            }
        }
    }

    public string DataDirectory { get; private set; } = string.Empty;
    public string LogsDirectory { get; private set; } = string.Empty;
    public string ConfigFilePath { get; private set; } = string.Empty;
    public string BackupsDirectory { get; private set; } = string.Empty;

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand OpenDataDirCommand { get; }

    public SettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService loc,
        ILogService log)
    {
        _settingsService = settingsService;
        _loc = loc;
        _log = log;
        Languages = loc.SupportedLanguages;

        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "1.0.0";
        if (version.Contains('+')) version = version[..version.IndexOf('+')];
        AppVersion = version;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetCommand = new AsyncRelayCommand(ResetAsync);
        OpenDataDirCommand = new RelayCommand(OpenDataDir);

        Settings = _settingsService.Current;
        _selectedLanguage = Languages.FirstOrDefault(l => l.CultureName == loc.CurrentCulture.Name);

        _loc.LanguageChanged += OnLanguageChanged;
    }

    private string _saveFeedback = string.Empty;
    public string SaveFeedback
    {
        get => _saveFeedback;
        set => SetProperty(ref _saveFeedback, value);
    }

    private DateTime _saveFeedbackAt = DateTime.MinValue;
    private void OnLanguageChanged(object? sender, CultureInfo e)
    {
        var match = Languages.FirstOrDefault(l => string.Equals(l.CultureName, e.Name, StringComparison.OrdinalIgnoreCase));
        if (match != null && !ReferenceEquals(match, _selectedLanguage))
        {
            _selectedLanguage = match;
            OnPropertyChanged(nameof(SelectedLanguage));
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            Settings = _settingsService.Current;
            DataDirectory = _settingsService.GetDataDirectory();
            LogsDirectory = _settingsService.GetLogsDirectory();
            ConfigFilePath = _settingsService.GetConfigFilePath();
            BackupsDirectory = _settingsService.GetBackupsDirectory();

            _selectedLanguage = Languages.FirstOrDefault(l => l.CultureName == _loc.CurrentCulture.Name);
            OnPropertyChanged(nameof(SelectedLanguage));
        }
        catch (Exception ex)
        {
            _log.Error("Settings", $"Init failed: {ex.Message}", ex);
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            var pendingLang = Settings.Language;
            var currentLang = _loc.CurrentCulture.Name;
            if (!string.Equals(pendingLang, currentLang, StringComparison.OrdinalIgnoreCase))
            {
                _loc.SetLanguage(pendingLang);
            }

            await _settingsService.UpdateAsync(s =>
            {
                s.Language = Settings.Language;
                s.ApiHost = Settings.ApiHost;
                s.ApiPort = Settings.ApiPort;
                s.CustomOllamaExecutable = Settings.CustomOllamaExecutable;
                s.CustomModelsDirectory = Settings.CustomModelsDirectory;
                s.Theme = Settings.Theme;
                s.RefreshIntervalOllamaSeconds = Settings.RefreshIntervalOllamaSeconds;
                s.RefreshIntervalRunningSeconds = Settings.RefreshIntervalRunningSeconds;
                s.RefreshIntervalGpuSeconds = Settings.RefreshIntervalGpuSeconds;
                s.RefreshIntervalModelsSeconds = Settings.RefreshIntervalModelsSeconds;
                s.HttpTimeoutSeconds = Settings.HttpTimeoutSeconds;
                s.StartupTimeoutSeconds = Settings.StartupTimeoutSeconds;
                s.ShutdownTimeoutSeconds = Settings.ShutdownTimeoutSeconds;
                s.MinimizeToTrayOnClose = Settings.MinimizeToTrayOnClose;
                s.StartMinimized = Settings.StartMinimized;
                s.StartWithWindows = Settings.StartWithWindows;
                s.LogLevel = Settings.LogLevel;
                s.MaxLogFileSizeMB = Settings.MaxLogFileSizeMB;
                s.MaxLogFiles = Settings.MaxLogFiles;
                s.ConfirmDangerousOperations = Settings.ConfirmDangerousOperations;
                s.EnableTelemetry = Settings.EnableTelemetry;
                s.UseNvidiaGpuMonitoring = Settings.UseNvidiaGpuMonitoring;
                s.AllowDeleteModels = Settings.AllowDeleteModels;
                s.AllowReleaseAllModels = Settings.AllowReleaseAllModels;
                s.OllamaStartupArgs = Settings.OllamaStartupArgs;
            });

            SaveFeedback = _loc["Settings.SaveSuccess"];
            _saveFeedbackAt = DateTime.Now;
            _ = ClearFeedbackAfterDelay();
        }
        catch (Exception ex)
        {
            _log.Error("Settings", $"Save failed: {ex.Message}", ex);
        }
    }

    private async Task ClearFeedbackAfterDelay()
    {
        try
        {
            await Task.Delay(3500);
            if ((DateTime.Now - _saveFeedbackAt).TotalMilliseconds >= 3000)
            {
                SaveFeedback = string.Empty;
            }
        }
        catch { }
    }

    private async Task ResetAsync()
    {
        try
        {
            await _settingsService.ResetToDefaultsAsync();
            Settings = _settingsService.Current;
        }
        catch (Exception ex)
        {
            _log.Error("Settings", $"Reset failed: {ex.Message}", ex);
        }
    }

    private void OpenDataDir()
    {
        try
        {
            var dir = _settingsService.GetDataDirectory();
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }
        catch (Exception ex)
        {
            _log.Warn("Settings", $"Open data dir failed: {ex.Message}");
        }
    }
}