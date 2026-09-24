using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IOllamaDiscoveryService
{
    Task<OllamaStatus> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<OllamaStatus> RefreshStatusAsync(CancellationToken cancellationToken = default);
    Task<OllamaStatus> SetCustomExecutableAsync(string path, CancellationToken cancellationToken = default);
    Task<OllamaStatus> SetApiEndpointAsync(string host, int port, CancellationToken cancellationToken = default);
    Task<OllamaStatus> SetModelsDirectoryAsync(string path, CancellationToken cancellationToken = default);
    IEnumerable<string> CommonOllamaPaths();
    bool FileExists(string path);
    string? FindOllamaInPath();
}
