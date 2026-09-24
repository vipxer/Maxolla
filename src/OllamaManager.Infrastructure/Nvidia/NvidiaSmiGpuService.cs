using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Exceptions;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Nvidia;

public class NvidiaSmiGpuService : IGpuMonitoringService
{
    private readonly IProcessService _processService;
    private readonly ILogger<NvidiaSmiGpuService> _logger;
    public string BackendName => "nvidia-smi";
    public bool IsAvailable { get; private set; }

    public NvidiaSmiGpuService(IProcessService processService, ILogger<NvidiaSmiGpuService> logger)
    {
        _processService = processService;
        _logger = logger;
        IsAvailable = CheckAvailability();
    }

    private bool CheckAvailability()
    {
        try
        {
            var whereResult = _processService.RunAsync("where.exe", new[] { "nvidia-smi" }, timeout: TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            return whereResult.Success;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<GpuInfo>> GetGpusAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return new List<GpuInfo>();
        try
        {
            var result = await _processService.RunAsync("nvidia-smi",
                new[] {
                    "--query-gpu=index,name,driver_version,temperature.gpu,utilization.gpu,memory.total,memory.used,power.draw,power.limit,clocks.current.graphics,clocks.current.memory",
                    "--format=csv,noheader,nounits"
                }, timeout: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Success) return new List<GpuInfo>();
            var gpus = new List<GpuInfo>();
            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                if (parts.Length < 11) continue;
                if (!int.TryParse(parts[0], out var idx)) continue;
                gpus.Add(new GpuInfo
                {
                    Index = idx,
                    Vendor = "NVIDIA",
                    Name = parts[1],
                    DriverVersion = parts[2],
                    Temperature = SafeParseDouble(parts[3]),
                    GpuUtilization = SafeParseDouble(parts[4]),
                    MemoryTotal = (long)(SafeParseDouble(parts[5]) * 1024 * 1024),
                    MemoryUsed = (long)(SafeParseDouble(parts[6]) * 1024 * 1024),
                    PowerUsage = SafeParseDouble(parts[7]),
                    PowerLimit = SafeParseDouble(parts[8]),
                    GraphicsClock = (long)SafeParseDouble(parts[9]),
                    MemoryClock = (long)SafeParseDouble(parts[10])
                });
            }

            foreach (var gpu in gpus)
            {
                try
                {
                    gpu.Processes = (await GetProcessesAsync(gpu.Index, cancellationToken).ConfigureAwait(false)).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get processes for GPU {Index}", gpu.Index);
                }
            }

            return gpus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query nvidia-smi");
            return new List<GpuInfo>();
        }
    }

    public async Task<GpuInfo?> GetPrimaryGpuAsync(CancellationToken cancellationToken = default)
    {
        var gpus = await GetGpusAsync(cancellationToken).ConfigureAwait(false);
        return gpus.FirstOrDefault();
    }

    public async Task<List<GpuProcessInfo>> GetProcessesAsync(int gpuIndex = 0, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return new List<GpuProcessInfo>();
        try
        {
            var result = await _processService.RunAsync("nvidia-smi",
                new[] {
                    $"--query-compute-apps=gpu_uuid,pid,process_name,used_memory",
                    "--format=csv,noheader,nounits"
                }, timeout: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Success) return new List<GpuProcessInfo>();
            var procs = new List<GpuProcessInfo>();
            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                if (parts.Length < 4) continue;
                procs.Add(new GpuProcessInfo
                {
                    Pid = SafeParseInt(parts[1]),
                    ProcessName = parts[2],
                    MemoryUsed = (long)(SafeParseDouble(parts[3]) * 1024 * 1024),
                    IsOllamaRelated = parts[2].Contains("ollama", StringComparison.OrdinalIgnoreCase) ||
                                     parts[2].Contains("ollama_llama", StringComparison.OrdinalIgnoreCase)
                });
            }
            return procs;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query GPU processes");
            return new List<GpuProcessInfo>();
        }
    }

    private static double SafeParseDouble(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "[Not Supported]" || s == "[N/A]") return 0;
        return double.TryParse(s.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
    }
    private static int SafeParseInt(string s) => (int)SafeParseDouble(s);
}
