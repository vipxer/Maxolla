using System.Collections.ObjectModel;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;
using SkiaSharp;

namespace OllamaManager.App.ViewModels;

public class BenchmarkViewModel : AsyncObservableObject
{
    private readonly IBenchmarkService _benchmarkService;
    private readonly IOllamaService _ollamaService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<BenchmarkResult> History { get; } = new();

    private string _selectedModel = string.Empty;
    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    private string _prompt = "Write a short story about a curious robot exploring a garden.";
    public string Prompt
    {
        get => _prompt;
        set => SetProperty(ref _prompt, value);
    }

    private int _numRuns = 3;
    public int NumRuns
    {
        get => _numRuns;
        set => SetProperty(ref _numRuns, value);
    }

    private int _warmupRuns = 1;
    public int WarmupRuns
    {
        get => _warmupRuns;
        set => SetProperty(ref _warmupRuns, value);
    }

    private int _maxTokens = 256;
    public int MaxTokens
    {
        get => _maxTokens;
        set => SetProperty(ref _maxTokens, value);
    }

    private string _progressMessage = string.Empty;
    public string ProgressMessage
    {
        get => _progressMessage;
        set => SetProperty(ref _progressMessage, value);
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    private BenchmarkResult? _latestResult;
    public BenchmarkResult? LatestResult
    {
        get => _latestResult;
        set
        {
            if (SetProperty(ref _latestResult, value))
            {
                OnPropertyChanged(nameof(LatestRunLabels));
                OnPropertyChanged(nameof(LatestRunTpsValues));
                OnPropertyChanged(nameof(LatestResultSummary));
                RebuildChart();
            }
        }
    }

    public double[] LatestRunTpsValues =>
        LatestResult?.TestRuns
            .Select(r => r.TokensPerSecond ?? 0)
            .ToArray() ?? Array.Empty<double>();

    public string[] LatestRunLabels =>
        LatestResult?.TestRuns
            .Select((r, i) => $"#{i + 1}")
            .ToArray() ?? Array.Empty<string>();

    public string LatestResultSummary
    {
        get
        {
            if (LatestResult == null) return string.Empty;
            var r = LatestResult;
            return $"{r.Model} · avg {r.AverageTokensPerSecond:F1} t/s · min {r.MinTokensPerSecond:F1} · max {r.MaxTokensPerSecond:F1}";
        }
    }

    private ISeries[] _tpsSeries = Array.Empty<ISeries>();
    public ISeries[] TpsSeries
    {
        get => _tpsSeries;
        private set => SetProperty(ref _tpsSeries, value);
    }

    private LiveChartsCore.Kernel.Sketches.ICartesianAxis[] _tpsXAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
    public LiveChartsCore.Kernel.Sketches.ICartesianAxis[] TpsXAxes
    {
        get => _tpsXAxes;
        private set => SetProperty(ref _tpsXAxes, value);
    }

    private LiveChartsCore.Kernel.Sketches.ICartesianAxis[] _tpsYAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
    public LiveChartsCore.Kernel.Sketches.ICartesianAxis[] TpsYAxes
    {
        get => _tpsYAxes;
        private set => SetProperty(ref _tpsYAxes, value);
    }

    private void RebuildChart()
    {
        if (LatestResult == null || LatestResult.TestRuns.Count == 0)
        {
            TpsSeries = Array.Empty<ISeries>();
            TpsXAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
            TpsYAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
            return;
        }
        var values = LatestResult.TestRuns
            .Select(r => r.TokensPerSecond ?? 0)
            .ToArray();
        TpsSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = values,
                Name = "t/s",
                Stroke = new SolidColorPaint(SKColor.Parse("#4CAF50")) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#4CAF50")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                Fill = null
            }
        };
        TpsXAxes = new LiveChartsCore.Kernel.Sketches.ICartesianAxis[]
        {
            new Axis
            {
                Name = "run",
                Labels = LatestResult.TestRuns.Select((r, i) => $"#{i + 1}").ToArray(),
                NamePaint = new SolidColorPaint(SKColors.Gray),
                LabelsPaint = new SolidColorPaint(SKColors.Gray)
            }
        };
        TpsYAxes = new LiveChartsCore.Kernel.Sketches.ICartesianAxis[]
        {
            new Axis
            {
                Name = "t/s",
                NamePaint = new SolidColorPaint(SKColors.Gray),
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                MinLimit = 0
            }
        };
    }

    private BenchmarkResult? _selectedHistoryItem;
    public BenchmarkResult? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set => SetProperty(ref _selectedHistoryItem, value);
    }

    public ICommand LoadModelsCommand { get; }
    public ICommand LoadHistoryCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand DeleteHistoryItemCommand { get; }
    public ICommand DeleteAllHistoryCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public BenchmarkViewModel(
        IBenchmarkService benchmarkService,
        IOllamaService ollamaService,
        ILocalizationService loc,
        ILogService log)
    {
        _benchmarkService = benchmarkService;
        _ollamaService = ollamaService;
        _loc = loc;
        _log = log;

        LoadModelsCommand = new AsyncRelayCommand(LoadModelsAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
        RunCommand = new AsyncRelayCommand(RunBenchmarkAsync, () => !string.IsNullOrEmpty(SelectedModel) && !IsBusy);
        DeleteHistoryItemCommand = new AsyncRelayCommand(DeleteHistoryItemAsync, () => SelectedHistoryItem != null);
        DeleteAllHistoryCommand = new AsyncRelayCommand(DeleteAllHistoryAsync, () => History.Count > 0);
        ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync, () => LatestResult != null);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, () => History.Count > 0);
    }

    public async Task InitializeAsync()
    {
        await LoadModelsAsync();
        await LoadHistoryAsync();
    }

    public async Task LoadModelsAsync()
    {
        try
        {
            var models = await _ollamaService.GetModelsAsync();
            AvailableModels.Clear();
            foreach (var m in models) AvailableModels.Add(m.Name);
            if (!string.IsNullOrEmpty(SelectedModel) && !AvailableModels.Contains(SelectedModel))
            {
                SelectedModel = AvailableModels.FirstOrDefault() ?? string.Empty;
            }
            else if (string.IsNullOrEmpty(SelectedModel))
            {
                SelectedModel = AvailableModels.FirstOrDefault() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _log.Warn("Benchmark", $"Load models failed: {ex.Message}");
        }
    }

    public async Task LoadHistoryAsync()
    {
        try
        {
            var items = await _benchmarkService.GetHistoryAsync();
            History.Clear();
            foreach (var h in items.OrderByDescending(x => x.Timestamp)) History.Add(h);
        }
        catch (Exception ex)
        {
            _log.Warn("Benchmark", $"Load history failed: {ex.Message}");
        }
    }

    private async Task RunBenchmarkAsync()
    {
        if (string.IsNullOrEmpty(SelectedModel)) return;

        var config = new BenchmarkConfiguration
        {
            Model = SelectedModel,
            Prompt = Prompt,
            TestRuns = NumRuns,
            WarmupRuns = WarmupRuns,
            MaxTokens = MaxTokens
        };

        var progress = new Progress<BenchmarkProgress>(p =>
        {
            ProgressMessage = $"{p.Stage} {p.CurrentRun}/{p.TotalRuns}";
            ProgressPercent = p.PercentComplete;
        });

        await RunBusyAsync(_loc["Benchmark.Running"], async () =>
        {
            try
            {
                LatestResult = await _benchmarkService.RunAsync(config, progress);
                ProgressMessage = _loc["Benchmark.Complete"];
                ProgressPercent = 100;
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                _log.Error("Benchmark", $"Run failed: {ex.Message}", ex);
                ProgressMessage = _loc["Benchmark.Failed"];
            }
        });
    }

    private async Task DeleteHistoryItemAsync()
    {
        if (SelectedHistoryItem == null) return;
        var confirm = System.Windows.MessageBox.Show(
            _loc["Benchmark.ConfirmDeleteItem"],
            _loc["Benchmark.DeleteItem"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            var item = SelectedHistoryItem;
            await _benchmarkService.DeleteHistoryItemAsync(item.Timestamp, item.Model);
            SelectedHistoryItem = null;
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            _log.Warn("Benchmark", $"Delete failed: {ex.Message}");
        }
    }

    private async Task DeleteAllHistoryAsync()
    {
        if (History.Count == 0) return;
        var confirm = System.Windows.MessageBox.Show(
            _loc["Benchmark.ConfirmDeleteAll"],
            _loc["Benchmark.DeleteAll"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            SelectedHistoryItem = null;
            await _benchmarkService.DeleteAllHistoryAsync();
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            _log.Error("Benchmark", $"Clear all failed: {ex.Message}", ex);
        }
    }

    private async Task ExportJsonAsync()
    {
        if (LatestResult == null) return;
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON|*.json",
                FileName = $"benchmark-{LatestResult.Model}-{LatestResult.Timestamp:yyyyMMddHHmmss}.json"
            };
            if (dlg.ShowDialog() == true)
            {
                await _benchmarkService.ExportJsonAsync(LatestResult, dlg.FileName);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Benchmark", $"Export JSON failed: {ex.Message}", ex);
        }
    }

    private async Task ExportCsvAsync()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV|*.csv",
                FileName = "benchmark-history.csv"
            };
            if (dlg.ShowDialog() == true)
            {
                await _benchmarkService.ExportCsvAsync(History, dlg.FileName);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Benchmark", $"Export CSV failed: {ex.Message}", ex);
        }
    }
}