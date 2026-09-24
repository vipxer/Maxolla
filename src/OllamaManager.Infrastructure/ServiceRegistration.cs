using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;
using OllamaManager.Infrastructure.Backup;
using OllamaManager.Infrastructure.Benchmark;
using OllamaManager.Infrastructure.Configuration;
using OllamaManager.Infrastructure.Diagnostics;
using OllamaManager.Infrastructure.Localization;
using OllamaManager.Infrastructure.Logging;
using OllamaManager.Infrastructure.Modelfile;
using OllamaManager.Infrastructure.Nvidia;
using OllamaManager.Infrastructure.Ollama;
using OllamaManager.Infrastructure.ProcessManagement;

namespace OllamaManager.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddOllamaManagerInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, FileSettingsService>();
        services.AddSingleton<ILocalizationService, JsonLocalizationService>();
        services.AddSingleton<FileLogService>();
        services.AddSingleton<ILogService>(sp => sp.GetRequiredService<FileLogService>());
        services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<FileLogService>());

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<FileLogService>());
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        });

        services.AddSingleton<IProcessService, WindowsProcessService>();

        services.AddHttpClient<IOllamaClient, OllamaHttpClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            var timeout = TimeSpan.FromSeconds(settings.Current.HttpTimeoutSeconds);
            client.Timeout = timeout;
            var url = $"http://{settings.Current.ApiHost}:{settings.Current.ApiPort}";
            client.BaseAddress = new Uri(url);
        });

        services.AddSingleton<IOllamaDiscoveryService, OllamaDiscoveryService>();
        services.AddSingleton<OllamaService>();
        services.AddSingleton<IOllamaService>(sp => sp.GetRequiredService<OllamaService>());
        services.AddSingleton<IModelService>(sp => sp.GetRequiredService<OllamaService>());
        services.AddSingleton<IModelfileService, ModelfileService>();
        services.AddSingleton<IBenchmarkService, BenchmarkService>();
        services.AddSingleton<IGpuMonitoringService, NvidiaSmiGpuService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
        services.AddSingleton<IBackupService, ZipBackupService>();

        return services;
    }
}
