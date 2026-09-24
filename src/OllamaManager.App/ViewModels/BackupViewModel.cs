using System.Collections.ObjectModel;
using System.Windows.Input;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class BackupViewModel : AsyncObservableObject
{
    private readonly IBackupService _backupService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public ObservableCollection<BackupInfo> Backups { get; } = new();

    private BackupInfo? _selectedBackup;
    public BackupInfo? SelectedBackup
    {
        get => _selectedBackup;
        set => SetProperty(ref _selectedBackup, value);
    }

    private bool _includeAppSettings = true;
    public bool IncludeAppSettings
    {
        get => _includeAppSettings;
        set => SetProperty(ref _includeAppSettings, value);
    }

    private bool _includeModelfiles = true;
    public bool IncludeModelfiles
    {
        get => _includeModelfiles;
        set => SetProperty(ref _includeModelfiles, value);
    }

    private bool _includeBenchmarkHistory = true;
    public bool IncludeBenchmarkHistory
    {
        get => _includeBenchmarkHistory;
        set => SetProperty(ref _includeBenchmarkHistory, value);
    }

    private bool _includeCustomConfigs = true;
    public bool IncludeCustomConfigs
    {
        get => _includeCustomConfigs;
        set => SetProperty(ref _includeCustomConfigs, value);
    }

    private bool _includeModelFiles;
    public bool IncludeModelFiles
    {
        get => _includeModelFiles;
        set => SetProperty(ref _includeModelFiles, value);
    }

    private string _progressMessage = string.Empty;
    public string ProgressMessage
    {
        get => _progressMessage;
        set => SetProperty(ref _progressMessage, value);
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public ICommand CreateBackupCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public BackupViewModel(
        IBackupService backupService,
        ILocalizationService loc,
        ILogService log)
    {
        _backupService = backupService;
        _loc = loc;
        _log = log;

        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshListAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteBackupAsync, () => SelectedBackup != null);
        RestoreCommand = new AsyncRelayCommand(RestoreBackupAsync, () => SelectedBackup != null);
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public async Task InitializeAsync()
    {
        await RefreshListAsync();
    }

    private async Task RefreshListAsync()
    {
        try
        {
            var list = await _backupService.ListBackupsAsync();
            Backups.Clear();
            foreach (var b in list.OrderByDescending(x => x.CreatedAt)) Backups.Add(b);
        }
        catch (Exception ex)
        {
            _log.Warn("Backup", $"Refresh failed: {ex.Message}");
        }
    }

    private async Task CreateBackupAsync()
    {
        var options = new BackupOptions
        {
            IncludeAppSettings = IncludeAppSettings,
            IncludeModelfiles = IncludeModelfiles,
            IncludeBenchmarkHistory = IncludeBenchmarkHistory,
            IncludeCustomConfigs = IncludeCustomConfigs,
            IncludeModelFiles = IncludeModelFiles
        };

        var progress = new Progress<BackupProgress>(p =>
        {
            ProgressMessage = p.CurrentFile;
            ProgressPercent = p.PercentComplete;
        });

        await RunBusyAsync(_loc["Backup.Creating"], async () =>
        {
            try
            {
                var backup = await _backupService.CreateBackupAsync(options, progress);
                ProgressMessage = _loc["Backup.Created"];
                await RefreshListAsync();
                SelectedBackup = Backups.FirstOrDefault(b => b.FilePath == backup.FilePath);
            }
            catch (Exception ex)
            {
                _log.Error("Backup", $"Create failed: {ex.Message}", ex);
                ProgressMessage = _loc["Backup.Failed"];
            }
        });
    }

    private async Task DeleteBackupAsync()
    {
        if (SelectedBackup == null) return;
        try
        {
            await _backupService.DeleteBackupAsync(SelectedBackup.FilePath);
            await RefreshListAsync();
        }
        catch (Exception ex)
        {
            _log.Error("Backup", $"Delete failed: {ex.Message}", ex);
        }
    }

    private async Task RestoreBackupAsync()
    {
        if (SelectedBackup == null) return;
        try
        {
            await _backupService.RestoreBackupAsync(SelectedBackup.FilePath,
                entry => RestoreAction.Overwrite,
                new Progress<BackupProgress>(p =>
                {
                    ProgressMessage = p.CurrentFile;
                    ProgressPercent = p.PercentComplete;
                }));
            ProgressMessage = _loc["Backup.Restored"];
        }
        catch (Exception ex)
        {
            _log.Error("Backup", $"Restore failed: {ex.Message}", ex);
            ProgressMessage = _loc["Backup.Failed"];
        }
    }

    private void OpenFolder()
    {
        try
        {
            var dir = _backupService.ListBackupsAsync().GetAwaiter().GetResult();
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OllamaManager", "Backups");
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            _log.Warn("Backup", $"Open folder failed: {ex.Message}");
        }
    }
}