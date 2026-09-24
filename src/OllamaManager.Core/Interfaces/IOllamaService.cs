using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IOllamaService
{
    IOllamaDiscoveryService Discovery { get; }
    IOllamaClient Client { get; }

    Task<OllamaStatus> GetCurrentStatusAsync(CancellationToken cancellationToken = default);
    Task<OllamaStatus> StartAsync(CancellationToken cancellationToken = default);
    Task<OllamaStatus> StopAsync(bool force = false, CancellationToken cancellationToken = default);
    Task<OllamaStatus> RestartAsync(CancellationToken cancellationToken = default);

    Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<List<RunningModelInfo>> GetRunningModelsAsync(CancellationToken cancellationToken = default);

    Task<bool> StartModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<bool> StopModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<bool> UnloadModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<int> ReleaseAllModelsAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteModelAsync(string modelName, CancellationToken cancellationToken = default);

    Task<ModelDetails?> GetModelDetailsAsync(string modelName, CancellationToken cancellationToken = default);
}
