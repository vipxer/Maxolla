using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IOllamaClient
{
    string BaseUrl { get; }

    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<List<RunningModelInfo>> GetRunningModelsAsync(CancellationToken cancellationToken = default);
    Task<ModelDetails?> GetModelDetailsAsync(string modelName, CancellationToken cancellationToken = default);

    Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
    Task<bool> CreateModelAsync(string name, string modelfileContent, CancellationToken cancellationToken = default);
    Task<bool> DeleteModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<bool> CopyModelAsync(string source, string destination, CancellationToken cancellationToken = default);
    Task<bool> PullModelAsync(string modelName, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    Task<bool> UnloadModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<bool> LoadModelAsync(string modelName, TimeSpan? keepAlive = null, CancellationToken cancellationToken = default);

    void SetBaseUrl(string baseUrl);
}
