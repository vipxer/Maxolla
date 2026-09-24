using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Backup;

public class ZipBackupService : IBackupService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ZipBackupService> _logger;
    public const int CurrentBackupVersion = 1;
    private const string ManifestName = "manifest.json";

    public ZipBackupService(ISettingsService settingsService, ILogger<ZipBackupService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<BackupInfo> CreateBackupAsync(BackupOptions options, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var targetDir = options.TargetDirectory ?? _settingsService.GetBackupsDirectory();
        Directory.CreateDirectory(targetDir);

        var name = options.BackupName ?? $"backup-{DateTime.Now:yyyyMMdd-HHmmss}";
        var fileName = name.EndsWith(".ombak", StringComparison.OrdinalIgnoreCase) ? name : name + ".ombak";
        var filePath = Path.Combine(targetDir, fileName);

        var entries = new List<BackupEntry>();
        var tmpFile = filePath + ".tmp";

        try
        {
            using var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            if (options.IncludeAppSettings)
            {
                var settingsFile = _settingsService.GetConfigFilePath();
                if (File.Exists(settingsFile))
                {
                    var entry = archive.CreateEntry("settings.json");
                    using var es = entry.Open();
                    using var fs2 = File.OpenRead(settingsFile);
                    await fs2.CopyToAsync(es, cancellationToken).ConfigureAwait(false);
                    entries.Add(new BackupEntry { RelativePath = "settings.json", SizeBytes = new FileInfo(settingsFile).Length, Type = BackupContentType.AppSettings });
                }
            }

            if (options.IncludeBenchmarkHistory)
            {
                var histFile = _settingsService.GetBenchmarkHistoryFilePath();
                if (File.Exists(histFile))
                {
                    var entry = archive.CreateEntry("benchmark-history.json");
                    using var es = entry.Open();
                    using var fs2 = File.OpenRead(histFile);
                    await fs2.CopyToAsync(es, cancellationToken).ConfigureAwait(false);
                    entries.Add(new BackupEntry { RelativePath = "benchmark-history.json", SizeBytes = new FileInfo(histFile).Length, Type = BackupContentType.BenchmarkHistory });
                }
            }

            if (options.IncludeModelfiles)
            {
                var modelfilesDir = Path.Combine(_settingsService.GetDataDirectory(), "Modelfiles");
                if (Directory.Exists(modelfilesDir))
                {
                    foreach (var file in Directory.GetFiles(modelfilesDir, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var rel = "modelfiles/" + Path.GetRelativePath(modelfilesDir, file).Replace('\\', '/');
                        var entry = archive.CreateEntry(rel);
                        using var es = entry.Open();
                        using var fs2 = File.OpenRead(file);
                        await fs2.CopyToAsync(es, cancellationToken).ConfigureAwait(false);
                        entries.Add(new BackupEntry { RelativePath = rel, SizeBytes = new FileInfo(file).Length, Type = BackupContentType.Modelfiles });
                    }
                }
            }

            if (options.IncludeModelFiles)
            {
                _logger.LogWarning("Model file backup is large; ensure sufficient disk space");
            }

            var info = new BackupInfo
            {
                FilePath = filePath,
                CreatedAt = DateTime.Now,
                BackupVersion = CurrentBackupVersion,
                ApplicationVersion = "1.0.0",
                Entries = entries,
                SizeBytes = 0,
                IncludeModelFiles = options.IncludeModelFiles
            };

            var manifestEntry = archive.CreateEntry(ManifestName);
            using (var ms = manifestEntry.Open())
            {
                var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
                var bytes = Encoding.UTF8.GetBytes(json);
                await ms.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (File.Exists(tmpFile)) try { File.Delete(tmpFile); } catch { }
            throw;
        }

        if (File.Exists(filePath)) File.Delete(filePath);
        File.Move(tmpFile, filePath);

        progress?.Report(new BackupProgress { CurrentFile = "Done", IsIndeterminate = false });

        return new BackupInfo
        {
            FilePath = filePath,
            CreatedAt = DateTime.Now,
            BackupVersion = CurrentBackupVersion,
            ApplicationVersion = "1.0.0",
            Entries = entries,
            SizeBytes = new FileInfo(filePath).Length,
            IncludeModelFiles = options.IncludeModelFiles
        };
    }

    public async Task<BackupInfo?> InspectBackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            using var fs = File.OpenRead(filePath);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
            var manifestEntry = archive.GetEntry(ManifestName);
            if (manifestEntry == null) return null;
            using var ms = manifestEntry.Open();
            var info = await JsonSerializer.DeserializeAsync<BackupInfo>(ms, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (info != null)
            {
                info.FilePath = filePath;
                info.SizeBytes = new FileInfo(filePath).Length;
            }
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect backup {File}", filePath);
            return null;
        }
    }

    public Task<List<BackupInfo>> ListBackupsAsync(string? directory = null, CancellationToken cancellationToken = default)
    {
        var dir = directory ?? _settingsService.GetBackupsDirectory();
        if (!Directory.Exists(dir)) return Task.FromResult(new List<BackupInfo>());
        var files = Directory.GetFiles(dir, "*.ombak").OrderByDescending(f => new FileInfo(f).LastWriteTime);
        var list = new List<BackupInfo>();
        foreach (var f in files)
        {
            try
            {
                var info = InspectBackupAsync(f, cancellationToken).GetAwaiter().GetResult();
                if (info != null) list.Add(info);
            }
            catch { }
        }
        return Task.FromResult(list);
    }

    public Task DeleteBackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }

    public async Task RestoreBackupAsync(string filePath, Func<BackupEntry, RestoreAction> conflictResolver, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Backup file not found", filePath);

        using var fs = File.OpenRead(filePath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry(ManifestName);
        if (manifestEntry != null)
        {
            using var ms = manifestEntry.Open();
            var info = await JsonSerializer.DeserializeAsync<BackupInfo>(ms, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (info != null && info.BackupVersion > CurrentBackupVersion)
            {
                throw new InvalidOperationException($"Backup version {info.BackupVersion} is newer than supported version {CurrentBackupVersion}");
            }
        }

        long totalBytes = archive.Entries.Where(e => e.FullName != ManifestName).Sum(e => e.Length);
        long processed = 0;

        foreach (var entry in archive.Entries.Where(e => e.FullName != ManifestName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localPath = ResolveRestorePath(entry.FullName);
            if (string.IsNullOrEmpty(localPath))
            {
                processed += entry.Length;
                continue;
            }

            if (File.Exists(localPath))
            {
                var backupEntry = new BackupEntry { RelativePath = entry.FullName, SizeBytes = entry.Length };
                var action = conflictResolver(backupEntry);
                if (action == RestoreAction.Cancel) break;
                if (action == RestoreAction.Skip)
                {
                    processed += entry.Length;
                    progress?.Report(new BackupProgress { CurrentFile = entry.FullName, BytesProcessed = processed, TotalBytes = totalBytes });
                    continue;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            using var es = entry.Open();
            using var os = File.Create(localPath);
            var buffer = new byte[81920];
            int read;
            while ((read = await es.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await os.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                processed += read;
                progress?.Report(new BackupProgress { CurrentFile = entry.FullName, BytesProcessed = processed, TotalBytes = totalBytes });
            }
        }
    }

    private string? ResolveRestorePath(string entryName)
    {
        if (entryName == "settings.json") return _settingsService.GetConfigFilePath();
        if (entryName == "benchmark-history.json") return _settingsService.GetBenchmarkHistoryFilePath();
        if (entryName.StartsWith("modelfiles/", StringComparison.OrdinalIgnoreCase))
        {
            var rel = entryName.Substring("modelfiles/".Length);
            return Path.Combine(_settingsService.GetDataDirectory(), "Modelfiles", rel);
        }
        return null;
    }
}
