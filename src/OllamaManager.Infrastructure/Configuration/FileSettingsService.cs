using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Configuration;

public class FileSettingsService : ISettingsService
{
    private readonly ILogger<FileSettingsService> _logger;
    private readonly string _settingsFile;
    private readonly string _dataDir;
    private readonly string _logsDir;
    private readonly string _cacheDir;
    private readonly string _backupsDir;
    private readonly string _benchmarkHistoryFile;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    public AppSettings Current { get; private set; } = new();
    public event EventHandler<AppSettings>? SettingsChanged;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    public FileSettingsService(ILogger<FileSettingsService> logger)
    {
        _logger = logger;

        var portableFlag = Path.Combine(AppContext.BaseDirectory, "portable.flag");
        if (File.Exists(portableFlag))
        {
            _dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dataDir = Path.Combine(localAppData, "OllamaManager");
        }

        _settingsFile = Path.Combine(_dataDir, "Config", "settings.json");
        _logsDir = Path.Combine(_dataDir, "Logs");
        _cacheDir = Path.Combine(_dataDir, "Cache");
        _backupsDir = Path.Combine(_dataDir, "Backups");
        _benchmarkHistoryFile = Path.Combine(_dataDir, "Benchmark", "history.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            Directory.CreateDirectory(_logsDir);
            Directory.CreateDirectory(_cacheDir);
            Directory.CreateDirectory(_backupsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(_benchmarkHistoryFile)!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create data directories");
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsFile))
            {
                Current = new AppSettings();
                await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_settingsFile, cancellationToken).ConfigureAwait(false);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (loaded != null)
                {
                    Current = MergeWithDefaults(loaded);
                }
                else
                {
                    Current = new AppSettings();
                    await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse settings.json — restoring defaults");
                var corruptPath = _settingsFile + $".corrupt.{DateTime.Now:yyyyMMddHHmmss}";
                try { File.Copy(_settingsFile, corruptPath, overwrite: true); } catch { }
                Current = new AppSettings();
                await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveInternalAsync(CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_settingsFile)!;
        Directory.CreateDirectory(dir);
        var tmp = _settingsFile + ".tmp";
        var json = JsonSerializer.Serialize(Current, _jsonOptions);
        await File.WriteAllTextAsync(tmp, json, cancellationToken).ConfigureAwait(false);
        if (File.Exists(_settingsFile))
        {
            File.Replace(tmp, _settingsFile, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, _settingsFile);
        }
    }

    public async Task UpdateAsync(Action<AppSettings> mutation, CancellationToken cancellationToken = default)
    {
        mutation(Current);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        SettingsChanged?.Invoke(this, Current);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        Current = new AppSettings();
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        SettingsChanged?.Invoke(this, Current);
    }

    public string GetDataDirectory() => _dataDir;
    public string GetLogsDirectory() => _logsDir;
    public string GetConfigFilePath() => _settingsFile;
    public string GetCacheDirectory() => _cacheDir;
    public string GetBackupsDirectory() => _backupsDir;
    public string GetBenchmarkHistoryFilePath() => _benchmarkHistoryFile;

    private static AppSettings MergeWithDefaults(AppSettings loaded)
    {
        var defaults = new AppSettings();
        var result = new AppSettings();

        foreach (var prop in typeof(AppSettings).GetProperties())
        {
            var loadedValue = prop.GetValue(loaded);
            var defaultValue = prop.GetValue(defaults);
            if (loadedValue == null || (loadedValue is string s && string.IsNullOrEmpty(s) && defaultValue is string ds && !string.IsNullOrEmpty(ds)))
            {
                prop.SetValue(result, defaultValue);
            }
            else
            {
                prop.SetValue(result, loadedValue);
            }
        }
        return result;
    }
}
