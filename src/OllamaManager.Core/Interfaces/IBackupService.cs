using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IBackupService
{
    Task<BackupInfo> CreateBackupAsync(BackupOptions options, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<BackupInfo?> InspectBackupAsync(string filePath, CancellationToken cancellationToken = default);
    Task<List<BackupInfo>> ListBackupsAsync(string? directory = null, CancellationToken cancellationToken = default);
    Task DeleteBackupAsync(string filePath, CancellationToken cancellationToken = default);
    Task RestoreBackupAsync(string filePath, Func<BackupEntry, RestoreAction> conflictResolver, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default);
}

public class BackupOptions
{
    public bool IncludeAppSettings { get; set; } = true;
    public bool IncludeModelfiles { get; set; } = true;
    public bool IncludeBenchmarkHistory { get; set; } = true;
    public bool IncludeCustomConfigs { get; set; } = true;
    public bool IncludeModelFiles { get; set; } = false;
    public string? TargetDirectory { get; set; }
    public string? BackupName { get; set; }
}
