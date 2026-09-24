using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Exceptions;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Ollama;

[SupportedOSPlatform("windows")]
public class OllamaDiscoveryService : IOllamaDiscoveryService
{
    private readonly IProcessService _processService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OllamaDiscoveryService> _logger;
    private OllamaStatus _cachedStatus = new();
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);

    public OllamaDiscoveryService(IProcessService processService, ISettingsService settingsService, ILogger<OllamaDiscoveryService> logger)
    {
        _processService = processService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<OllamaStatus> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        await _discoveryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cachedStatus = await PerformDiscoveryAsync(cancellationToken).ConfigureAwait(false);
            return _cachedStatus;
        }
        finally
        {
            _discoveryLock.Release();
        }
    }

    public async Task<OllamaStatus> RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        return await DiscoverAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OllamaStatus> SetCustomExecutableAsync(string path, CancellationToken cancellationToken = default)
    {
        await _settingsService.UpdateAsync(s => s.CustomOllamaExecutable = path, cancellationToken).ConfigureAwait(false);
        return await DiscoverAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OllamaStatus> SetApiEndpointAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await _settingsService.UpdateAsync(s =>
        {
            s.ApiHost = host;
            s.ApiPort = port;
        }, cancellationToken).ConfigureAwait(false);
        return await DiscoverAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OllamaStatus> SetModelsDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        await _settingsService.UpdateAsync(s => s.CustomModelsDirectory = path, cancellationToken).ConfigureAwait(false);
        return await DiscoverAsync(cancellationToken).ConfigureAwait(false);
    }

    public IEnumerable<string> CommonOllamaPaths()
    {
        var paths = new List<string>();

        var customExe = _settingsService.Current.CustomOllamaExecutable;
        if (!string.IsNullOrWhiteSpace(customExe))
        {
            paths.Add(customExe);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        paths.Add(Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe"));
        paths.Add(Path.Combine(localAppData, "Programs", "Ollama", "Ollama.exe"));

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        paths.Add(Path.Combine(programFiles, "Ollama", "ollama.exe"));

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        paths.Add(Path.Combine(programFilesX86, "Ollama", "ollama.exe"));

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        paths.Add(Path.Combine(userHome, "AppData", "Local", "Programs", "Ollama", "ollama.exe"));

        var windowsApps = Path.Combine(userHome, "AppData", "Local", "Microsoft", "WindowsApps", "ollama.exe");
        paths.Add(windowsApps);

        var winget = Path.Combine(userHome, "AppData", "Local", "Microsoft", "WinGet", "Links", "ollama.exe");
        paths.Add(winget);

        return paths.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public bool FileExists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public string? FindOllamaInPath()
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var separator = Path.PathSeparator;
            var paths = pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var dir in paths)
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), "ollama.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // ignore invalid PATH entries
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate PATH");
        }
        return null;
    }

    private async Task<OllamaStatus> PerformDiscoveryAsync(CancellationToken cancellationToken)
    {
        var status = new OllamaStatus
        {
            LastChecked = DateTime.Now,
            ApiEndpoint = $"http://{_settingsService.Current.ApiHost}:{_settingsService.Current.ApiPort}"
        };

        try
        {
            var customExe = _settingsService.Current.CustomOllamaExecutable;
            if (!string.IsNullOrWhiteSpace(customExe) && File.Exists(customExe))
            {
                status.ExecutablePath = customExe;
                status.DiscoveryMethod = "UserSelected";
            }
            else
            {
                var fromPath = FindOllamaInPath();
                if (!string.IsNullOrEmpty(fromPath))
                {
                    status.ExecutablePath = fromPath;
                    status.DiscoveryMethod = "PATH";
                }
                else
                {
                    var fromProcess = _processService.GetExecutablePath("ollama");
                    if (!string.IsNullOrEmpty(fromProcess))
                    {
                        status.ExecutablePath = fromProcess;
                        status.DiscoveryMethod = "RunningProcess";
                    }
                    else
                    {
                        foreach (var commonPath in CommonOllamaPaths())
                        {
                            if (File.Exists(commonPath))
                            {
                                status.ExecutablePath = commonPath;
                                status.DiscoveryMethod = "DefaultPath";
                                break;
                            }
                        }
                    }
                }
            }

            status.ModelsDirectory = ResolveModelsDirectory();

            if (string.IsNullOrEmpty(status.ExecutablePath) || !File.Exists(status.ExecutablePath))
            {
                status.Kind = OllamaStatusKind.NotInstalled;
                status.ErrorMessage = "Ollama executable not found";
                return status;
            }

            status.Kind = OllamaStatusKind.InstalledButNotRunning;

            var processes = _processService.FindProcesses("ollama").ToList();
            if (processes.Any())
            {
                status.Kind = OllamaStatusKind.RunningButApiUnavailable;
                status.Version = await TryGetVersionAsync(status.ExecutablePath, cancellationToken).ConfigureAwait(false);

                var apiOk = await TryPingApiAsync(cancellationToken).ConfigureAwait(false);
                if (apiOk)
                {
                    status.Kind = OllamaStatusKind.ApiAvailable;
                    if (string.IsNullOrEmpty(status.Version))
                    {
                        status.Version = await TryGetApiVersionAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                status.Version = await TryGetVersionAsync(status.ExecutablePath, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            status.Kind = OllamaStatusKind.Unknown;
            status.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Ollama discovery failed");
        }

        return status;
    }

    private string? ResolveModelsDirectory()
    {
        var custom = _settingsService.Current.CustomModelsDirectory;
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }
        var envModels = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
        if (!string.IsNullOrWhiteSpace(envModels))
        {
            return envModels;
        }
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultPath = Path.Combine(userHome, ".ollama", "models");
        return defaultPath;
    }

    private async Task<bool> TryPingApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var url = $"http://{_settingsService.Current.ApiHost}:{_settingsService.Current.ApiPort}/api/tags";
            var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> TryGetApiVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var url = $"http://{_settingsService.Current.ApiHost}:{_settingsService.Current.ApiPort}/api/version";
            var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("version", out var v))
                {
                    return v.GetString();
                }
            }
            catch { }
        }
        catch { }
        return null;
    }

    private async Task<string?> TryGetVersionAsync(string exePath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processService.RunAsync(exePath, new[] { "--version" }, timeout: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                var version = (result.StandardOutput + result.StandardError).Trim();
                return string.IsNullOrEmpty(version) ? null : version;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get ollama version");
        }
        return null;
    }
}
