using Microsoft.Extensions.Logging;
using OllamaManager.Core.Exceptions;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Ollama;

public class OllamaService : IOllamaService, IModelService
{
    private readonly IProcessService _processService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OllamaService> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public IOllamaDiscoveryService Discovery { get; }
    public IOllamaClient Client { get; }

    public OllamaService(
        IOllamaDiscoveryService discovery,
        IOllamaClient client,
        IProcessService processService,
        ISettingsService settingsService,
        ILogger<OllamaService> logger)
    {
        Discovery = discovery;
        Client = client;
        _processService = processService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<OllamaStatus> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        return await Discovery.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OllamaStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var status = await Discovery.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.IsApiAvailable)
            {
                return status;
            }
            if (string.IsNullOrEmpty(status.ExecutablePath) || !File.Exists(status.ExecutablePath))
            {
                throw new OllamaNotFoundException();
            }

            _logger.LogInformation("Starting Ollama from {Path}", status.ExecutablePath);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = status.ExecutablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var startupArgs = _settingsService.Current.OllamaStartupArgs;
            if (!string.IsNullOrWhiteSpace(startupArgs))
            {
                foreach (var a in startupArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    startInfo.ArgumentList.Add(a);
                }
            }

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                throw new OllamaManagerException("Failed to launch ollama.exe process");
            }

            var timeout = TimeSpan.FromSeconds(_settingsService.Current.StartupTimeoutSeconds);
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                if (await Client.PingAsync(cancellationToken).ConfigureAwait(false))
                {
                    _logger.LogInformation("Ollama API is now available");
                    return await Discovery.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            throw new OllamaTimeoutException($"Ollama did not respond within {timeout.TotalSeconds:N0} seconds");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<OllamaStatus> StopAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ollamaProcs = _processService.FindProcesses("ollama").ToList();
            if (ollamaProcs.Count == 0)
            {
                _logger.LogInformation("No Ollama process found to stop");
                return await Discovery.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var proc in ollamaProcs)
            {
                _logger.LogInformation("Stopping Ollama process pid={Pid}", proc.Pid);
                _processService.StopProcessTree(proc.Pid, force: force, timeout: TimeSpan.FromSeconds(_settingsService.Current.ShutdownTimeoutSeconds));
            }

            var deadline = DateTime.UtcNow.AddSeconds(_settingsService.Current.ShutdownTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                if (_processService.FindProcesses("ollama").Count() == 0)
                {
                    break;
                }
            }

            return await Discovery.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<OllamaStatus> RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(force: false, cancellationToken).ConfigureAwait(false);
        return await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var models = await Client.GetModelsAsync(cancellationToken).ConfigureAwait(false);
        var running = await Client.GetRunningModelsAsync(cancellationToken).ConfigureAwait(false);
        var runningNames = new HashSet<string>(running.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var m in models)
        {
            m.Status = runningNames.Contains(m.Name) ? ModelLifecycleStatus.Running : ModelLifecycleStatus.NotLoaded;
        }
        return models;
    }

    public async Task<List<RunningModelInfo>> GetRunningModelsAsync(CancellationToken cancellationToken = default)
    {
        return await Client.GetRunningModelsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> StartModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return false;
        var ok = await Client.LoadModelAsync(modelName, keepAlive: TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
        return ok;
    }

    public async Task<bool> StopModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        return await Client.UnloadModelAsync(modelName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UnloadModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        return await Client.UnloadModelAsync(modelName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ReleaseAllModelsAsync(CancellationToken cancellationToken = default)
    {
        var running = await Client.GetRunningModelsAsync(cancellationToken).ConfigureAwait(false);
        int released = 0;
        foreach (var r in running)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var ok = await Client.UnloadModelAsync(r.Name, cancellationToken).ConfigureAwait(false);
            if (ok) released++;
        }
        return released;
    }

    Task<int> IModelService.ReleaseAllRunningModelsAsync(CancellationToken cancellationToken) => ReleaseAllModelsAsync(cancellationToken);

    Task<bool> IModelService.PullModelAsync(string modelName, IProgress<string>? progress, CancellationToken cancellationToken) =>
        Client.PullModelAsync(modelName, progress, cancellationToken);

    public async Task<bool> DeleteModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        return await Client.DeleteModelAsync(modelName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ModelDetails?> GetModelDetailsAsync(string modelName, CancellationToken cancellationToken = default)
    {
        return await Client.GetModelDetailsAsync(modelName, cancellationToken).ConfigureAwait(false);
    }
}
