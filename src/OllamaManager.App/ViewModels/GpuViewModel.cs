using System.Collections.ObjectModel;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;
using SkiaSharp;

namespace OllamaManager.App.ViewModels;

public class GpuViewModel : AsyncObservableObject, IDisposable
{
    private readonly IGpuMonitoringService _gpuService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;
    private readonly System.Windows.Threading.DispatcherTimer _historyTimer;
    private const int HistoryCapacity = 60;

    public ObservableCollection<GpuInfo> Gpus { get; } = new();
    public ObservableCollection<GpuProcessInfo> Processes { get; } = new();

    private readonly ObservableCollection<double> _utilHistory = new();
    private readonly ObservableCollection<double> _memHistory = new();
    private readonly ObservableCollection<double> _tempHistory = new();

    public ObservableCollection<double> UtilHistory => _utilHistory;
    public ObservableCollection<double> MemHistory => _memHistory;
    public ObservableCollection<double> TempHistory => _tempHistory;

    private ISeries[] _utilSeries = Array.Empty<ISeries>();
    public ISeries[] UtilSeries
    {
        get => _utilSeries;
        private set => SetProperty(ref _utilSeries, value);
    }

    private ISeries[] _memSeries = Array.Empty<ISeries>();
    public ISeries[] MemSeries
    {
        get => _memSeries;
        private set => SetProperty(ref _memSeries, value);
    }

    private ICartesianAxis[] _historyXAxes = Array.Empty<ICartesianAxis>();
    public ICartesianAxis[] HistoryXAxes
    {
        get => _historyXAxes;
        private set => SetProperty(ref _historyXAxes, value);
    }

    private ICartesianAxis[] _utilYAxes = Array.Empty<ICartesianAxis>();
    public ICartesianAxis[] UtilYAxes
    {
        get => _utilYAxes;
        private set => SetProperty(ref _utilYAxes, value);
    }

    private ICartesianAxis[] _memYAxes = Array.Empty<ICartesianAxis>();
    public ICartesianAxis[] MemYAxes
    {
        get => _memYAxes;
        private set => SetProperty(ref _memYAxes, value);
    }

    private bool _historyEnabled;
    public bool HistoryEnabled
    {
        get => _historyEnabled;
        set
        {
            if (SetProperty(ref _historyEnabled, value))
            {
                if (value)
                {
                    _historyTimer.Start();
                    RebuildSeries();
                }
                else
                {
                    _historyTimer.Stop();
                    _utilHistory.Clear();
                    _memHistory.Clear();
                    _tempHistory.Clear();
                    UtilSeries = Array.Empty<ISeries>();
                    MemSeries = Array.Empty<ISeries>();
                }
            }
        }
    }

    private string _backendName = "Unknown";
    public string BackendName
    {
        get => _backendName;
        set => SetProperty(ref _backendName, value);
    }

    private bool _isAvailable;
    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetProperty(ref _isAvailable, value);
    }

    private GpuInfo? _selectedGpu;
    public GpuInfo? SelectedGpu
    {
        get => _selectedGpu;
        set
        {
            if (SetProperty(ref _selectedGpu, value))
            {
                _ = LoadProcessesAsync();
            }
        }
    }

    public ICommand RefreshCommand { get; }

    public GpuViewModel(IGpuMonitoringService gpuService, ILocalizationService loc, ILogService log)
    {
        _gpuService = gpuService;
        _loc = loc;
        _log = log;
        RefreshCommand = new AsyncRelayCommand(LoadGpuInfoAsync, () => !IsBusy);

        _historyTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _historyTimer.Tick += async (_, _) => await SampleHistoryAsync();
        RebuildSeries();
    }

    public void Dispose()
    {
        _historyTimer.Stop();
    }

    private async Task SampleHistoryAsync()
    {
        try
        {
            var gpus = await _gpuService.GetGpusAsync();
            var g = gpus.FirstOrDefault();
            if (g == null) return;
            AppendBounded(_utilHistory, g.GpuUtilization);
            AppendBounded(_memHistory, g.MemoryUtilizationPercent);
            AppendBounded(_tempHistory, g.Temperature);
        }
        catch (Exception ex)
        {
            _log.Warn("Gpu", $"History sample failed: {ex.Message}");
        }
    }

    private static void AppendBounded(ObservableCollection<double> col, double v)
    {
        col.Add(v);
        while (col.Count > HistoryCapacity) col.RemoveAt(0);
    }

    private void RebuildSeries()
    {
        UtilSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = _utilHistory,
                Name = "Util%",
                Stroke = new SolidColorPaint(SKColor.Parse("#4CAF50")) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#4CAF50")) { StrokeThickness = 2 },
                Fill = null
            }
        };
        MemSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = _memHistory,
                Name = "Mem%",
                Stroke = new SolidColorPaint(SKColor.Parse("#FF9800")) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#FF9800")) { StrokeThickness = 2 },
                Fill = null
            }
        };
        HistoryXAxes = new ICartesianAxis[]
        {
            new Axis
            {
                NamePaint = new SolidColorPaint(SKColors.Gray),
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                ShowSeparatorLines = false
            }
        };
        UtilYAxes = new ICartesianAxis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                NamePaint = new SolidColorPaint(SKColors.Gray),
                LabelsPaint = new SolidColorPaint(SKColors.Gray)
            }
        };
        MemYAxes = new ICartesianAxis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                NamePaint = new SolidColorPaint(SKColors.Gray),
                LabelsPaint = new SolidColorPaint(SKColors.Gray)
            }
        };
    }

    public async Task LoadGpuInfoAsync()
    {
        await RunBusyAsync(_loc["Gpu.Loading"], async () =>
        {
            try
            {
                BackendName = _gpuService.BackendName;
                IsAvailable = _gpuService.IsAvailable;

                var list = await _gpuService.GetGpusAsync();
                Gpus.Clear();
                foreach (var g in list) Gpus.Add(g);

                if (SelectedGpu == null && Gpus.Count > 0)
                {
                    SelectedGpu = Gpus[0];
                }
                else
                {
                    await LoadProcessesAsync();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Gpu", $"Load failed: {ex.Message}", ex);
            }
        });
    }

    private async Task LoadProcessesAsync()
    {
        if (SelectedGpu == null)
        {
            Processes.Clear();
            return;
        }
        try
        {
            var procs = await _gpuService.GetProcessesAsync(SelectedGpu.Index);
            Processes.Clear();
            foreach (var p in procs) Processes.Add(p);
        }
        catch (Exception ex)
        {
            _log.Warn("Gpu", $"Process load failed: {ex.Message}");
        }
    }
}