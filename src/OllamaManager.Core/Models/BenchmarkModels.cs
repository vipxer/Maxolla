namespace OllamaManager.Core.Models;

public class BenchmarkConfiguration
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = "Explain the concept of machine learning in simple terms.";
    public int MaxTokens { get; set; } = 256;
    public int WarmupRuns { get; set; } = 1;
    public int TestRuns { get; set; } = 3;
    public bool AutoUnloadAfter { get; set; } = true;
    public string? SystemPrompt { get; set; }
}

public class BenchmarkRun
{
    public int RunIndex { get; set; }
    public bool IsWarmup { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long? TotalDurationNs { get; set; }
    public long? LoadDurationNs { get; set; }
    public int? PromptEvalCount { get; set; }
    public long? PromptEvalDurationNs { get; set; }
    public int? EvalCount { get; set; }
    public long? EvalDurationNs { get; set; }
    public string? ResponseText { get; set; }

    public double? TokensPerSecond =>
        EvalCount is > 0 && EvalDurationNs is > 0
            ? EvalCount.Value * 1_000_000_000.0 / EvalDurationNs.Value
            : null;

    public double TotalDurationSeconds => TotalDurationNs is > 0 ? TotalDurationNs.Value / 1_000_000_000.0 : 0;
}

public class BenchmarkResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Model { get; set; } = string.Empty;
    public string? OllamaVersion { get; set; }
    public string? WindowsVersion { get; set; }
    public string? GpuName { get; set; }
    public string? GpuDriver { get; set; }
    public long? PeakVram { get; set; }
    public BenchmarkConfiguration Configuration { get; set; } = new();
    public List<BenchmarkRun> Runs { get; set; } = new();
    public List<BenchmarkRun> TestRuns => Runs.Where(r => !r.IsWarmup).ToList();
    public double? AverageTokensPerSecond => TestRuns.Where(r => r.Success && r.TokensPerSecond.HasValue).Select(r => r.TokensPerSecond!.Value).DefaultIfEmpty().Average();
    public double? MinTokensPerSecond => TestRuns.Where(r => r.Success && r.TokensPerSecond.HasValue).Select(r => r.TokensPerSecond!.Value).DefaultIfEmpty().Min();
    public double? MaxTokensPerSecond => TestRuns.Where(r => r.Success && r.TokensPerSecond.HasValue).Select(r => r.TokensPerSecond!.Value).DefaultIfEmpty().Max();
    public double? MedianTokensPerSecond
    {
        get
        {
            var vals = TestRuns.Where(r => r.Success && r.TokensPerSecond.HasValue).Select(r => r.TokensPerSecond!.Value).OrderBy(v => v).ToList();
            if (vals.Count == 0) return null;
            var mid = vals.Count / 2;
            return vals.Count % 2 == 0 ? (vals[mid - 1] + vals[mid]) / 2.0 : vals[mid];
        }
    }
}
