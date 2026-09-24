using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IModelService
{
    Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<List<RunningModelInfo>> GetRunningModelsAsync(CancellationToken cancellationToken = default);
    Task<ModelDetails?> GetModelDetailsAsync(string modelName, CancellationToken cancellationToken = default);
    Task<bool> DeleteModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<int> ReleaseAllRunningModelsAsync(CancellationToken cancellationToken = default);
    Task<bool> UnloadModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<bool> StartModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<bool> PullModelAsync(string modelName, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public interface IModelfileService
{
    Task<Modelfile> ParseAsync(string text, CancellationToken cancellationToken = default);
    string Serialize(Modelfile modelfile);
    Task<ModelfileValidation> ValidateAsync(string text, CancellationToken cancellationToken = default);
    Task<List<ModelfileParameter>> GetAvailableParametersAsync();
    Task<string> GenerateFromParametersAsync(string from, Dictionary<string, object> parameters, string? system = null, string? template = null, CancellationToken cancellationToken = default);
    Task<bool> CreateModelAsync(string name, string modelfileText, CancellationToken cancellationToken = default);
    Task<string?> ReadModelfileFromDiskAsync(string path, CancellationToken cancellationToken = default);
    Task WriteModelfileToDiskAsync(string path, string content, CancellationToken cancellationToken = default);
}
