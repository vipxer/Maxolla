using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OllamaManager.App.ViewModels;
using OllamaManager.Core.Interfaces;
using OllamaManager.Infrastructure;

namespace OllamaManager.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var services = new ServiceCollection();
            services.AddOllamaManagerInfrastructure();

            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ModelsViewModel>();
            services.AddTransient<RunningViewModel>();
            services.AddTransient<ChatViewModel>();
            services.AddTransient<ModelfileViewModel>();
            services.AddTransient<BenchmarkViewModel>();
            services.AddTransient<GpuViewModel>();
            services.AddTransient<DiagnosticsViewModel>();
            services.AddTransient<BackupViewModel>();
            services.AddTransient<LogsViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddSingleton<MainViewModel>();
            services.AddTransient<MainWindow>();

            Services = services.BuildServiceProvider();

            var settingsService = Services.GetRequiredService<ISettingsService>();
            await settingsService.LoadAsync();

            var loc = Services.GetRequiredService<ILocalizationService>();
            var culture = CultureInfo.GetCultureInfo(settingsService.Current.Language);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            if (settingsService.Current.StartMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
            }
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            MessageBox.Show(TryGetLocalized("App.StartupFailed", $"Startup failed:\n\n{{0}}", ex.Message),
                TryGetLocalized("App.Title", "Ollama Manager"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string TryGetLocalized(string key, string fallbackTemplate, params object[] args)
    {
        try
        {
            var loc = Services?.GetService<ILocalizationService>();
            if (loc != null)
            {
                var template = loc[key];
                if (args.Length > 0)
                {
                    try { return string.Format(template, args); } catch { return template; }
                }
                return template;
            }
        }
        catch { }
        try
        {
            return args.Length > 0 ? string.Format(fallbackTemplate, args) : fallbackTemplate;
        }
        catch
        {
            return fallbackTemplate;
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            var settings = Services.GetService<ISettingsService>();
            if (settings != null)
            {
                await settings.SaveAsync();
            }
        }
        catch { }
        base.OnExit(e);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash(ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(TryGetLocalized("App.UnexpectedError",
                "An unexpected error occurred:\n\n{0}\n\nThe error has been logged.",
                e.Exception.Message),
            TryGetLocalized("App.Title", "Ollama Manager"),
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        e.SetObserved();
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var crashDir = Path.Combine(localAppData, "OllamaManager", "Crash");
            Directory.CreateDirectory(crashDir);
            var file = Path.Combine(crashDir, $"crash-{DateTime.Now:yyyyMMddHHmmss}.log");
            File.WriteAllText(file, ex.ToString());
        }
        catch { }
    }
}