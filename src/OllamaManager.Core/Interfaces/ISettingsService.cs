using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler<AppSettings>? SettingsChanged;

    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Action<AppSettings> mutation, CancellationToken cancellationToken = default);
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);

    string GetDataDirectory();
    string GetLogsDirectory();
    string GetConfigFilePath();
    string GetCacheDirectory();
    string GetBackupsDirectory();
    string GetBenchmarkHistoryFilePath();
}
