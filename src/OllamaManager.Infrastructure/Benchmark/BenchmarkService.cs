using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Benchmark;

public class BenchmarkService : IBenchmarkService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly ISettingsService _settingsService;
    private readonly IGpuMonitoringService _gpuService;
    private readonly ILogService _logService;
    private readonly ILogger<BenchmarkService> _logger;
    private static readonly SemaphoreSlim _writeLock = new(1, 1);

    public BenchmarkService(
        IOllamaClient ollamaClient,
        ISettingsService settingsService,
        IGpuMonitoringService gpuService,
        ILogService logService,
        ILogger<BenchmarkService> logger)
    {
        _ollamaClient = ollamaClient;
        _settingsService = settingsService;
        _gpuService = gpuService;
        _logService = logService;
        _logger = logger;
    }

    public async Task<BenchmarkResult> RunAsync(BenchmarkConfiguration config, IProgress<BenchmarkProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.Model))
        {
            throw new ArgumentException("Model is required", nameof(config));
        }

        var result = new BenchmarkResult
        {
            Model = config.Model,
            Configuration = config,
            Timestamp = DateTime.Now,
        };

        try
        {
            result.OllamaVersion = await _ollamaClient.GetVersionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch { }

        result.WindowsVersion = Environment.OSVersion.VersionString;
        try
        {
            var primaryGpu = await _gpuService.GetPrimaryGpuAsync(cancellationToken).ConfigureAwait(false);
            if (primaryGpu != null)
            {
                result.GpuName = primaryGpu.Name;
                result.GpuDriver = primaryGpu.DriverVersion;
            }
        }
        catch { }

        long peakVram = 0;
        long startVram = 0;
        try
        {
            var running = await _ollamaClient.GetRunningModelsAsync(cancellationToken).ConfigureAwait(false);
            startVram = running.Sum(r => r.SizeVram);
        }
        catch { }

        int totalRuns = Math.Max(0, config.WarmupRuns) + Math.Max(1, config.TestRuns);
        int runCounter = 0;

        for (int i = 0; i < config.WarmupRuns; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runCounter++;
            progress?.Report(new BenchmarkProgress
            {
                Stage = "Warmup",
                CurrentRun = runCounter,
                TotalRuns = totalRuns,
                Message = $"Warmup run {i + 1}"
            });
            var run = await ExecuteSingleRunAsync(config, isWarmup: true, cancellationToken).ConfigureAwait(false);
            result.Runs.Add(run);
            if (run.Success && run.LoadDurationNs.HasValue && run.EvalCount.HasValue)
            {
                try
                {
                    var running = await _ollamaClient.GetRunningModelsAsync(cancellationToken).ConfigureAwait(false);
                    peakVram = Math.Max(peakVram, running.Sum(r => r.SizeVram));
                }
                catch { }
            }
        }

        for (int i = 0; i < config.TestRuns; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runCounter++;
            progress?.Report(new BenchmarkProgress
            {
                Stage = "Test",
                CurrentRun = runCounter,
                TotalRuns = totalRuns,
                Message = $"Test run {i + 1}/{config.TestRuns}"
            });
            var run = await ExecuteSingleRunAsync(config, isWarmup: false, cancellationToken).ConfigureAwait(false);
            result.Runs.Add(run);
            if (run.Success)
            {
                try
                {
                    var running = await _ollamaClient.GetRunningModelsAsync(cancellationToken).ConfigureAwait(false);
                    peakVram = Math.Max(peakVram, running.Sum(r => r.SizeVram));
                }
                catch { }
            }
        }

        result.PeakVram = Math.Max(startVram, peakVram);

        progress?.Report(new BenchmarkProgress
        {
            Stage = "Finalize",
            CurrentRun = totalRuns,
            TotalRuns = totalRuns,
            Message = "Saving result"
        });

        if (config.AutoUnloadAfter)
        {
            try
            {
                await _ollamaClient.UnloadModelAsync(config.Model, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto unload failed for {Model}", config.Model);
            }
        }

        try
        {
            await SaveResultAsync(result, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save benchmark result");
        }

        return result;
    }

    private async Task<BenchmarkRun> ExecuteSingleRunAsync(BenchmarkConfiguration config, bool isWarmup, CancellationToken cancellationToken)
    {
        var run = new BenchmarkRun { IsWarmup = isWarmup };
        try
        {
            var request = new GenerateRequest
            {
                Model = config.Model,
                Prompt = config.Prompt,
                Stream = false,
                System = config.SystemPrompt,
                Options = new Dictionary<string, object>
                {
                    ["num_predict"] = config.MaxTokens
                }
            };
            var response = await _ollamaClient.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
            run.Success = true;
            run.ResponseText = response.Response;
            run.TotalDurationNs = response.TotalDuration;
            run.LoadDurationNs = response.LoadDuration;
            run.PromptEvalCount = response.PromptEvalCount;
            run.PromptEvalDurationNs = response.PromptEvalDuration;
            run.EvalCount = response.EvalCount;
            run.EvalDurationNs = response.EvalDuration;
        }
        catch (Exception ex)
        {
            run.Success = false;
            run.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Benchmark run failed");
        }
        return run;
    }

    public async Task<List<BenchmarkResult>> GetHistoryAsync(string? modelFilter = null, CancellationToken cancellationToken = default)
    {
        var path = _settingsService.GetBenchmarkHistoryFilePath();
        if (!File.Exists(path)) return new List<BenchmarkResult>();
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var all = JsonSerializer.Deserialize<List<BenchmarkResult>>(json) ?? new();
            if (!string.IsNullOrWhiteSpace(modelFilter))
            {
                return all.Where(r => string.Equals(r.Model, modelFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return all;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read benchmark history");
            return new List<BenchmarkResult>();
        }
    }

    public async Task DeleteHistoryItemAsync(DateTime timestamp, string model, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = await GetHistoryAsync(null, cancellationToken).ConfigureAwait(false);
            list.RemoveAll(r => r.Timestamp == timestamp && r.Model == model);
            var path = _settingsService.GetBenchmarkHistoryFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
            if (File.Exists(path))
            {
                File.Replace(tmp, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteAllHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = _settingsService.GetBenchmarkHistoryFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            _logger.LogInformation("All benchmark history cleared");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveResultAsync(BenchmarkResult result, CancellationToken cancellationToken = default)
    {
        if (result == null) return;
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = await GetHistoryAsync(null, cancellationToken).ConfigureAwait(false);
            list.Add(result);
            if (list.Count > 500) list = list.OrderByDescending(r => r.Timestamp).Take(500).ToList();
            var path = _settingsService.GetBenchmarkHistoryFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
            if (File.Exists(path))
            {
                File.Replace(tmp, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ExportJsonAsync(BenchmarkResult result, string path, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportCsvAsync(IEnumerable<BenchmarkResult> results, string path, CancellationToken cancellationToken = default)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Timestamp,Model,AvgTokensPerSecond,MinTokensPerSecond,MaxTokensPerSecond,PromptEvalCount,EvalCount,PeakVram,OllamaVersion,WindowsVersion,GpuName");
        foreach (var r in results)
        {
            sb.AppendLine($"{r.Timestamp:O},{Escape(r.Model)},{r.AverageTokensPerSecond},{r.MinTokensPerSecond},{r.MaxTokensPerSecond},{r.TestRuns.FirstOrDefault()?.PromptEvalCount},{r.TestRuns.FirstOrDefault()?.EvalCount},{r.PeakVram},{Escape(r.OllamaVersion)},{Escape(r.WindowsVersion)},{Escape(r.GpuName)}");
        }
        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private static string Escape(string? s) => string.IsNullOrEmpty(s) ? string.Empty : "\"" + s.Replace("\"", "\"\"") + "\"";
}
