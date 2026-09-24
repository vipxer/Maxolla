using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IBenchmarkService
{
    Task<BenchmarkResult> RunAsync(BenchmarkConfiguration config, IProgress<BenchmarkProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<List<BenchmarkResult>> GetHistoryAsync(string? modelFilter = null, CancellationToken cancellationToken = default);
    Task DeleteHistoryItemAsync(DateTime timestamp, string model, CancellationToken cancellationToken = default);
    Task DeleteAllHistoryAsync(CancellationToken cancellationToken = default);
    Task SaveResultAsync(BenchmarkResult result, CancellationToken cancellationToken = default);
    Task ExportJsonAsync(BenchmarkResult result, string path, CancellationToken cancellationToken = default);
    Task ExportCsvAsync(IEnumerable<BenchmarkResult> results, string path, CancellationToken cancellationToken = default);
}

public class BenchmarkProgress
{
    public string Stage { get; set; } = string.Empty;
    public int CurrentRun { get; set; }
    public int TotalRuns { get; set; }
    public string Message { get; set; } = string.Empty;
    public double PercentComplete => TotalRuns > 0 ? (CurrentRun * 100.0 / TotalRuns) : 0;
}
