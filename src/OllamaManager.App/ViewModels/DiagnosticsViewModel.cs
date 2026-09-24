using System.Collections.ObjectModel;
using System.Windows.Input;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class DiagnosticsViewModel : AsyncObservableObject
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public ObservableCollection<DiagnosticItem> Items { get; } = new();

    private DiagnosticReport? _report;
    public DiagnosticReport? Report
    {
        get => _report;
        set => SetProperty(ref _report, value);
    }

    private string _reportText = string.Empty;
    public string ReportText
    {
        get => _reportText;
        set => SetProperty(ref _reportText, value);
    }

    private int _errorCount;
    public int ErrorCount
    {
        get => _errorCount;
        set => SetProperty(ref _errorCount, value);
    }

    private int _warningCount;
    public int WarningCount
    {
        get => _warningCount;
        set => SetProperty(ref _warningCount, value);
    }

    private int _okCount;
    public int OkCount
    {
        get => _okCount;
        set => SetProperty(ref _okCount, value);
    }

    public ICommand RunCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CopyCommand { get; }

    public DiagnosticsViewModel(
        IDiagnosticsService diagnosticsService,
        ILocalizationService loc,
        ILogService log)
    {
        _diagnosticsService = diagnosticsService;
        _loc = loc;
        _log = log;

        RunCommand = new AsyncRelayCommand(RunDiagnosticAsync, () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveReportAsync, () => Report != null);
        CopyCommand = new AsyncRelayCommand(CopyReportAsync, () => !string.IsNullOrEmpty(ReportText));
    }

    public async Task InitializeAsync()
    {
        if (Report == null) await RunDiagnosticAsync();
    }

    private async Task RunDiagnosticAsync()
    {
        await RunBusyAsync(_loc["Diagnostics.Running"], async () =>
        {
            try
            {
                Report = await _diagnosticsService.RunFullDiagnosticAsync();
                Items.Clear();
                foreach (var item in Report.Items) Items.Add(item);
                ErrorCount = Report.ErrorCount;
                WarningCount = Report.WarningCount;
                OkCount = Report.OkCount;
                ReportText = _diagnosticsService.FormatReportAsText(Report, true);
            }
            catch (Exception ex)
            {
                _log.Error("Diagnostics", $"Run failed: {ex.Message}", ex);
            }
        });
    }

    private async Task SaveReportAsync()
    {
        if (Report == null) return;
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text|*.txt|JSON|*.json",
                FileName = $"diagnostic-{DateTime.Now:yyyyMMddHHmmss}.txt"
            };
            if (dlg.ShowDialog() != true) return;
            var content = dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? _diagnosticsService.FormatReportAsJson(Report, true)
                : _diagnosticsService.FormatReportAsText(Report, true);
            await System.IO.File.WriteAllTextAsync(dlg.FileName, content);
        }
        catch (Exception ex)
        {
            _log.Error("Diagnostics", $"Save failed: {ex.Message}", ex);
        }
    }

    private async Task CopyReportAsync()
    {
        try
        {
            System.Windows.Clipboard.SetText(ReportText);
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            _log.Warn("Diagnostics", $"Copy failed: {ex.Message}");
        }
    }
}