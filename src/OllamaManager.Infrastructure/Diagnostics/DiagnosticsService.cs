using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Diagnostics;

public class DiagnosticsService : IDiagnosticsService
{
    private readonly IOllamaService _ollamaService;
    private readonly IGpuMonitoringService _gpuService;
    private readonly ISettingsService _settingsService;
    private readonly IProcessService _processService;
    private readonly ILogger<DiagnosticsService> _logger;

    public DiagnosticsService(
        IOllamaService ollamaService,
        IGpuMonitoringService gpuService,
        ISettingsService settingsService,
        IProcessService processService,
        ILogger<DiagnosticsService> logger)
    {
        _ollamaService = ollamaService;
        _gpuService = gpuService;
        _settingsService = settingsService;
        _processService = processService;
        _logger = logger;
    }

    public async Task<DiagnosticReport> RunFullDiagnosticAsync(CancellationToken cancellationToken = default)
    {
        var report = new DiagnosticReport();

        report.Items.AddRange(await CheckOllamaAsync(cancellationToken).ConfigureAwait(false));
        report.Items.AddRange(await CheckModelsAsync(cancellationToken).ConfigureAwait(false));
        report.Items.AddRange(CheckWindows());
        report.Items.AddRange(await CheckGpuAsync(cancellationToken).ConfigureAwait(false));
        report.Items.AddRange(await CheckNetworkAsync(cancellationToken).ConfigureAwait(false));

        return report;
    }

    private async Task<List<DiagnosticItem>> CheckOllamaAsync(CancellationToken cancellationToken)
    {
        var items = new List<DiagnosticItem>();
        try
        {
            var status = await _ollamaService.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
            items.Add(new DiagnosticItem
            {
                Category = "Ollama",
                Title = "Ollama Installation",
                Severity = !string.IsNullOrEmpty(status.ExecutablePath) ? DiagnosticSeverity.Ok : DiagnosticSeverity.Error,
                Detail = status.ExecutablePath ?? "Executable not found",
                Recommendation = string.IsNullOrEmpty(status.ExecutablePath) ? "Install Ollama or set the path in Settings" : null
            });
            items.Add(new DiagnosticItem
            {
                Category = "Ollama",
                Title = "API Endpoint",
                Severity = status.IsApiAvailable ? DiagnosticSeverity.Ok : DiagnosticSeverity.Warning,
                Detail = status.ApiEndpoint,
                Recommendation = status.IsApiAvailable ? null : "Make sure Ollama is running and accessible at the configured endpoint"
            });
            if (!string.IsNullOrEmpty(status.Version))
            {
                items.Add(new DiagnosticItem
                {
                    Category = "Ollama",
                    Title = "Version",
                    Severity = DiagnosticSeverity.Info,
                    Detail = status.Version
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new DiagnosticItem
            {
                Category = "Ollama",
                Title = "Ollama Status",
                Severity = DiagnosticSeverity.Error,
                Detail = ex.Message
            });
        }
        return items;
    }

    private async Task<List<DiagnosticItem>> CheckModelsAsync(CancellationToken cancellationToken)
    {
        var items = new List<DiagnosticItem>();
        try
        {
            var modelsDir = (await _ollamaService.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false)).ModelsDirectory;
            var dirExists = !string.IsNullOrEmpty(modelsDir) && Directory.Exists(modelsDir);
            items.Add(new DiagnosticItem
            {
                Category = "Models",
                Title = "Models Directory",
                Severity = dirExists ? DiagnosticSeverity.Ok : DiagnosticSeverity.Warning,
                Detail = modelsDir ?? "Unknown"
            });

            if (_ollamaService.GetCurrentStatusAsync(cancellationToken).Result.IsApiAvailable)
            {
                var models = await _ollamaService.GetModelsAsync(cancellationToken).ConfigureAwait(false);
                items.Add(new DiagnosticItem
                {
                    Category = "Models",
                    Title = "Model Count",
                    Severity = DiagnosticSeverity.Info,
                    Detail = $"{models.Count} model(s) installed"
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new DiagnosticItem
            {
                Category = "Models",
                Title = "Models Check",
                Severity = DiagnosticSeverity.Error,
                Detail = ex.Message
            });
        }
        return items;
    }

    private List<DiagnosticItem> CheckWindows()
    {
        var items = new List<DiagnosticItem>();
        items.Add(new DiagnosticItem
        {
            Category = "Windows",
            Title = "Operating System",
            Severity = DiagnosticSeverity.Info,
            Detail = Environment.OSVersion.VersionString
        });
        items.Add(new DiagnosticItem
        {
            Category = "Windows",
            Title = "Architecture",
            Severity = DiagnosticSeverity.Info,
            Detail = RuntimeInformation.OSArchitecture.ToString()
        });
        var isAdmin = IsRunAsAdministrator();
        items.Add(new DiagnosticItem
        {
            Category = "Windows",
            Title = "Administrator Privileges",
            Severity = isAdmin ? DiagnosticSeverity.Info : DiagnosticSeverity.Info,
            Detail = isAdmin ? "Yes (running elevated)" : "No (normal user)"
        });
        items.Add(new DiagnosticItem
        {
            Category = "Windows",
            Title = "User Directory",
            Severity = DiagnosticSeverity.Info,
            Detail = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        });
        items.Add(new DiagnosticItem
        {
            Category = "Windows",
            Title = "Temp Directory",
            Severity = DiagnosticSeverity.Info,
            Detail = Path.GetTempPath()
        });
        return items;
    }

    private async Task<List<DiagnosticItem>> CheckGpuAsync(CancellationToken cancellationToken)
    {
        var items = new List<DiagnosticItem>();
        try
        {
            var gpus = await _gpuService.GetGpusAsync(cancellationToken).ConfigureAwait(false);
            if (gpus.Count == 0)
            {
                items.Add(new DiagnosticItem
                {
                    Category = "GPU",
                    Title = "NVIDIA GPU",
                    Severity = DiagnosticSeverity.Warning,
                    Detail = "No NVIDIA GPU detected via nvidia-smi",
                    Recommendation = "GPU monitoring requires NVIDIA drivers"
                });
            }
            else
            {
                foreach (var gpu in gpus)
                {
                    items.Add(new DiagnosticItem
                    {
                        Category = "GPU",
                        Title = $"GPU {gpu.Index}",
                        Severity = DiagnosticSeverity.Ok,
                        Detail = $"{gpu.Name} | Driver {gpu.DriverVersion}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            items.Add(new DiagnosticItem
            {
                Category = "GPU",
                Title = "GPU Detection",
                Severity = DiagnosticSeverity.Error,
                Detail = ex.Message
            });
        }
        return items;
    }

    private async Task<List<DiagnosticItem>> CheckNetworkAsync(CancellationToken cancellationToken)
    {
        var items = new List<DiagnosticItem>();
        try
        {
            var settings = _settingsService.Current;
            var host = settings.ApiHost;
            var port = settings.ApiPort;

            var tcpOk = await IsTcpPortOpenAsync(host, port, cancellationToken).ConfigureAwait(false);
            items.Add(new DiagnosticItem
            {
                Category = "Network",
                Title = "TCP Port",
                Severity = tcpOk ? DiagnosticSeverity.Ok : DiagnosticSeverity.Warning,
                Detail = $"{host}:{port}"
            });

            if (tcpOk)
            {
                var httpOk = await IsHttpReachableAsync($"http://{host}:{port}/api/tags", cancellationToken).ConfigureAwait(false);
                items.Add(new DiagnosticItem
                {
                    Category = "Network",
                    Title = "HTTP API",
                    Severity = httpOk ? DiagnosticSeverity.Ok : DiagnosticSeverity.Warning,
                    Detail = $"http://{host}:{port}/api/tags"
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new DiagnosticItem
            {
                Category = "Network",
                Title = "Network Check",
                Severity = DiagnosticSeverity.Error,
                Detail = ex.Message
            });
        }
        return items;
    }

    private static async Task<bool> IsTcpPortOpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient { ReceiveTimeout = 1500, SendTimeout = 1500 };
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(2000, cancellationToken)).ConfigureAwait(false);
            if (completed != connectTask) return false;
            await connectTask.ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsHttpReachableAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRunAsAdministrator()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public string FormatReportAsText(DiagnosticReport report, bool anonymize = true)
    {
        var text = report.ToPlainText();
        if (anonymize) text = Anonymize(text);
        return text;
    }

    public string FormatReportAsJson(DiagnosticReport report, bool anonymize = true)
    {
        if (anonymize)
        {
            foreach (var item in report.Items)
            {
                item.Detail = Anonymize(item.Detail);
                item.Recommendation = Anonymize(item.Recommendation);
                item.Title = Anonymize(item.Title);
            }
        }
        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Anonymize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            input = input.Replace(userProfile, "C:\\Users\\<USER>");
        }
        input = Regex.Replace(input, @"C:\\Users\\[^\\\s]+", "C:\\Users\\<USER>");
        input = Regex.Replace(input, @"/Users/[^/\s]+", "/Users/<USER>");
        return input;
    }
}
