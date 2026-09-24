namespace OllamaManager.Core.Models;

public enum BackupContentType
{
    AppSettings,
    Modelfiles,
    BenchmarkHistory,
    CustomConfigurations
}

public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public DateTime CreatedAt { get; set; }
    public int BackupVersion { get; set; } = 1;
    public string? ApplicationVersion { get; set; }
    public List<BackupEntry> Entries { get; set; } = new();
    public long SizeBytes { get; set; }
    public string DisplaySize => ModelInfo.FormatBytes(SizeBytes);
    public bool IncludeModelFiles { get; set; }
}

public class BackupEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string DisplaySize => ModelInfo.FormatBytes(SizeBytes);
    public BackupContentType Type { get; set; }
}

public class BackupProgress
{
    public string CurrentFile { get; set; } = string.Empty;
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public double PercentComplete => TotalBytes > 0 ? (BytesProcessed * 100.0 / TotalBytes) : 0;
    public bool IsIndeterminate { get; set; }
}

public enum RestoreAction
{
    Overwrite,
    Skip,
    Cancel
}
