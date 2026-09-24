using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IGpuMonitoringService
{
    string BackendName { get; }
    bool IsAvailable { get; }

    Task<List<GpuInfo>> GetGpusAsync(CancellationToken cancellationToken = default);
    Task<GpuInfo?> GetPrimaryGpuAsync(CancellationToken cancellationToken = default);
    Task<List<GpuProcessInfo>> GetProcessesAsync(int gpuIndex = 0, CancellationToken cancellationToken = default);
}
