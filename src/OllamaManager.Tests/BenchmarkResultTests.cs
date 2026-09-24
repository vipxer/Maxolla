using OllamaManager.Core.Models;

namespace OllamaManager.Tests;

public class BenchmarkResultTests
{
    private static BenchmarkRun MakeRun(int index, bool warmup, int? evalCount, long? evalDurationNs)
    {
        return new BenchmarkRun
        {
            RunIndex = index,
            IsWarmup = warmup,
            Success = true,
            EvalCount = evalCount,
            EvalDurationNs = evalDurationNs
        };
    }

    [Fact]
    public void TestRuns_FiltersOutWarmupRuns()
    {
        var result = new BenchmarkResult
        {
            Model = "test",
            Runs = new List<BenchmarkRun>
            {
                MakeRun(1, true, 100, 10_000_000_000L),
                MakeRun(2, false, 100, 2_000_000_000L),
                MakeRun(3, false, 100, 1_666_666_666L),
            }
        };
        Assert.Equal(2, result.TestRuns.Count);
    }

    [Fact]
    public void AverageTokensPerSecond_CalculatesAverage()
    {
        var result = new BenchmarkResult
        {
            Model = "test",
            Runs = new List<BenchmarkRun>
            {
                MakeRun(1, false, 100, 2_500_000_000L),  // 40 tps
                MakeRun(2, false, 100, 1_666_666_666L),  // 60 tps
            }
        };
        Assert.NotNull(result.AverageTokensPerSecond);
        Assert.Equal(50.0, result.AverageTokensPerSecond!.Value, 0);
    }

    [Fact]
    public void MedianTokensPerSecond_OddCount_ReturnsMiddle()
    {
        var result = new BenchmarkResult
        {
            Model = "test",
            Runs = new List<BenchmarkRun>
            {
                MakeRun(1, false, 100, 10_000_000_000L),   // 10 tps
                MakeRun(2, false, 100, 3_333_333_333L),   // 30 tps
                MakeRun(3, false, 100, 2_000_000_000L),   // 50 tps
            }
        };
        Assert.NotNull(result.MedianTokensPerSecond);
        Assert.Equal(30.0, result.MedianTokensPerSecond!.Value, 0);
    }

    [Fact]
    public void TokensPerSecond_CalculatesFromInputs()
    {
        var r = MakeRun(1, false, 100, 1_000_000_000L);
        Assert.NotNull(r.TokensPerSecond);
        Assert.Equal(100.0, r.TokensPerSecond!.Value, 0);
    }

    [Fact]
    public void TokensPerSecond_NoInputs_ReturnsNull()
    {
        var r = new BenchmarkRun { RunIndex = 1, IsWarmup = false, Success = true };
        Assert.Null(r.TokensPerSecond);
    }
}