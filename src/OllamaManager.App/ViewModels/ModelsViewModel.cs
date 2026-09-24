using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class ModelsViewModel : AsyncObservableObject
{
    private readonly IOllamaService _ollamaService;
    private readonly IModelService _modelService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public ObservableCollection<ModelInfo> Models { get; } = new();
    public ObservableCollection<ModelInfo> FilteredModels { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    private ModelInfo? _selectedModel;
    public ModelInfo? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value))
            {
                _ = LoadDetailsAsync();
            }
        }
    }

    private ModelDetails? _selectedDetails;
    public ModelDetails? SelectedDetails
    {
        get => _selectedDetails;
        set => SetProperty(ref _selectedDetails, value);
    }

    private string _pullModelName = string.Empty;
    public string PullModelName
    {
        get => _pullModelName;
        set => SetProperty(ref _pullModelName, value);
    }

    private string _pullProgress = string.Empty;
    public string PullProgress
    {
        get => _pullProgress;
        set => SetProperty(ref _pullProgress, value);
    }

    private bool _isPulling;
    public bool IsPulling
    {
        get => _isPulling;
        set => SetProperty(ref _isPulling, value);
    }

    private string _operationStatus = string.Empty;
    public string OperationStatus
    {
        get => _operationStatus;
        set => SetProperty(ref _operationStatus, value);
    }

    private bool _isOperating;
    public bool IsOperating
    {
        get => _isOperating;
        set => SetProperty(ref _isOperating, value);
    }

    public event Action<string>? ModelLoaded;

    public ICommand LoadModelsCommand { get; }
    public ICommand DeleteModelCommand { get; }
    public ICommand PullModelCommand { get; }
    public ICommand ShowModelfileCommand { get; }
    public ICommand RunModelCommand { get; }
    public ICommand StopModelCommand { get; }
    public ICommand UnloadModelCommand { get; }
    public ICommand ReleaseAllModelsCommand { get; }
    public ICommand RefreshStatusCommand { get; }
    public ICommand BenchmarkSelectedCommand { get; }
    public ICommand CopyNameCommand { get; }
    public ICommand QuickStartCommand { get; }
    public ICommand PreloadAllCommand { get; }

    public ModelsViewModel(IOllamaService ollamaService, IModelService modelService, ILocalizationService loc, ILogService log)
    {
        _ollamaService = ollamaService;
        _modelService = modelService;
        _loc = loc;
        _log = log;

        LoadModelsCommand = new AsyncRelayCommand(LoadModelsAsync);
        DeleteModelCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedModel != null);
        PullModelCommand = new AsyncRelayCommand(PullModelAsync, () => !string.IsNullOrWhiteSpace(PullModelName) && !IsPulling);
        ShowModelfileCommand = new AsyncRelayCommand(ShowModelfileAsync, () => SelectedModel != null);
        RunModelCommand = new AsyncRelayCommand(RunSelectedAsync, () => SelectedModel != null && !IsOperating);
        StopModelCommand = new AsyncRelayCommand(StopSelectedAsync, () => SelectedModel != null && SelectedModel.Status == ModelLifecycleStatus.Running && !IsOperating);
        UnloadModelCommand = new AsyncRelayCommand(UnloadSelectedAsync, () => SelectedModel != null && SelectedModel.Status == ModelLifecycleStatus.Running && !IsOperating);
        ReleaseAllModelsCommand = new AsyncRelayCommand(ReleaseAllAsync, () => !IsOperating);
        RefreshStatusCommand = new AsyncRelayCommand(RefreshStatusAsync);
        BenchmarkSelectedCommand = new RelayCommand(BenchmarkSelected, () => SelectedModel != null);
        CopyNameCommand = new RelayCommand(CopyName, () => SelectedModel != null);
        QuickStartCommand = new RelayCommand(p => QuickStartByIndex(p));
        PreloadAllCommand = new AsyncRelayCommand(PreloadAllAsync, () => Models.Count > 0 && !IsOperating);
    }

    public async Task InitializeAsync()
    {
        await LoadModelsAsync();
    }

    public async Task LoadModelsAsync()
    {
        try
        {
            var status = await _ollamaService.GetCurrentStatusAsync();
            if (!status.IsApiAvailable)
            {
                Models.Clear();
                FilteredModels.Clear();
                return;
            }

            var models = await _ollamaService.GetModelsAsync();
            var running = await _ollamaService.GetRunningModelsAsync();
            var runningSet = new HashSet<string>(running.Select(r => r.Name));

            var existing = Models.ToDictionary(m => m.Name, StringComparer.Ordinal);
            int idx = 0;
            foreach (var m in models)
            {
                m.Status = runningSet.Contains(m.Name)
                    ? ModelLifecycleStatus.Running
                    : ModelLifecycleStatus.NotLoaded;
                m.Category = ModelInfo.ClassifyCategory(m.Name);
                m.ItemNumber = ++idx;

                if (existing.TryGetValue(m.Name, out var prev))
                {
                    var i = Models.IndexOf(prev);
                    if (i >= 0) Models[i] = m;
                }
                else
                {
                    Models.Add(m);
                }
            }
            for (int i = Models.Count - 1; i >= 0; i--)
            {
                if (!models.Any(x => x.Name == Models[i].Name)) Models.RemoveAt(i);
            }
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _log.Error("Models", $"Load failed: {ex.Message}", ex);
        }
    }

    private void ApplyFilter()
    {
        FilteredModels.Clear();
        var query = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? Models
            : Models.Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var m in filtered) FilteredModels.Add(m);
    }

    private async Task LoadDetailsAsync()
    {
        if (SelectedModel == null)
        {
            SelectedDetails = null;
            return;
        }
        try
        {
            SelectedDetails = await _ollamaService.GetModelDetailsAsync(SelectedModel.Name);
        }
        catch (Exception ex)
        {
            _log.Warn("Models", $"Details failed: {ex.Message}");
            SelectedDetails = null;
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedModel == null) return;
        var name = SelectedModel.Name;
        try
        {
            await _modelService.DeleteModelAsync(name);
            await LoadModelsAsync();
        }
        catch (Exception ex)
        {
            _log.Error("Models", $"Delete failed: {ex.Message}", ex);
        }
    }

    private async Task PullModelAsync()
    {
        if (string.IsNullOrWhiteSpace(PullModelName)) return;
        IsPulling = true;
        PullProgress = _loc["Models.Starting"];
        try
        {
            var progress = new Progress<string>(s => PullProgress = s);
            var ok = await _modelService.PullModelAsync(PullModelName.Trim(), progress);
            PullProgress = ok ? _loc["Models.PullComplete"] : _loc["Models.PullFailed"];
            if (ok) await LoadModelsAsync();
        }
        catch (Exception ex)
        {
            PullProgress = $"{_loc["Models.PullFailed"]}: {ex.Message}";
            _log.Error("Models", $"Pull failed: {ex.Message}", ex);
        }
        finally
        {
            IsPulling = false;
        }
    }

    private async Task ShowModelfileAsync()
    {
        if (SelectedModel == null) return;
        try
        {
            var details = await _ollamaService.GetModelDetailsAsync(SelectedModel.Name);
            if (details == null) return;
            SelectedDetails = details;
        }
        catch (Exception ex)
        {
            _log.Warn("Models", $"Modelfile fetch failed: {ex.Message}");
        }
    }

    private async Task RunSelectedAsync()
    {
        if (SelectedModel == null) return;
        var name = SelectedModel.Name;
        IsOperating = true;
        OperationStatus = $"{_loc["Models.LoadingHint"]} ({name})";
        SelectedModel.Status = ModelLifecycleStatus.Loading;
        try
        {
            var ok = await _ollamaService.StartModelAsync(name);
            OperationStatus = ok
                ? $"{name}: {_loc["Models.RunSucceeded"]}"
                : $"{name}: {_loc["Models.RunFailed"]}";
            if (ok)
            {
                ModelLoaded?.Invoke(name);
            }
        }
        catch (Exception ex)
        {
            OperationStatus = $"{name}: {_loc["Models.RunFailed"]} ({ex.Message})";
            _log.Error("Models", $"Run failed: {ex.Message}", ex);
            SelectedModel.Status = ModelLifecycleStatus.Error;
        }
        await RefreshStatusAsync();
        IsOperating = false;
        _ = ClearStatusAfterDelay();
    }

    private async Task StopSelectedAsync()
    {
        if (SelectedModel == null) return;
        var name = SelectedModel.Name;
        IsOperating = true;
        OperationStatus = $"{_loc["Models.Stopping"]} {name}...";
        SelectedModel.Status = ModelLifecycleStatus.Stopping;
        try
        {
            var ok = await _ollamaService.StopModelAsync(name);
            OperationStatus = ok
                ? $"{name}: {_loc["Models.StopSucceeded"]}"
                : $"{name}: {_loc["Models.StopFailed"]}";
        }
        catch (Exception ex)
        {
            OperationStatus = $"{name}: {_loc["Models.StopFailed"]} ({ex.Message})";
            _log.Error("Models", $"Stop failed: {ex.Message}", ex);
        }
        await RefreshStatusAsync();
        IsOperating = false;
        _ = ClearStatusAfterDelay();
    }

    private async Task UnloadSelectedAsync()
    {
        if (SelectedModel == null) return;
        var name = SelectedModel.Name;
        IsOperating = true;
        OperationStatus = $"{_loc["Models.Unloading"]} {name}...";
        try
        {
            var ok = await _ollamaService.UnloadModelAsync(name);
            OperationStatus = ok
                ? $"{name}: {_loc["Models.UnloadSucceeded"]}"
                : $"{name}: {_loc["Models.UnloadFailed"]}";
        }
        catch (Exception ex)
        {
            OperationStatus = $"{name}: {_loc["Models.UnloadFailed"]} ({ex.Message})";
            _log.Error("Models", $"Unload failed: {ex.Message}", ex);
        }
        await RefreshStatusAsync();
        IsOperating = false;
        _ = ClearStatusAfterDelay();
    }

    private async Task ReleaseAllAsync()
    {
        var confirm = System.Windows.MessageBox.Show(
            _loc["Models.ConfirmReleaseAll"],
            _loc["Models.ReleaseAll"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        IsOperating = true;
        OperationStatus = _loc["Models.ReleasingAll"] + "...";
        try
        {
            var released = await _ollamaService.ReleaseAllModelsAsync();
            OperationStatus = $"{_loc["Models.ReleaseAll"]}: {released}";
            _log.Info("Models", $"Released {released} models");
        }
        catch (Exception ex)
        {
            OperationStatus = $"{_loc["Models.ReleaseAll"]} {_loc["Models.Failed"]} ({ex.Message})";
            _log.Error("Models", $"Release all failed: {ex.Message}", ex);
        }
        await RefreshStatusAsync();
        IsOperating = false;
        _ = ClearStatusAfterDelay();
    }

    public async Task RefreshStatusAsync()
    {
        try
        {
            var running = await _ollamaService.GetRunningModelsAsync();
            var runningSet = new HashSet<string>(running.Select(r => r.Name));
            foreach (var m in Models)
            {
                var target = runningSet.Contains(m.Name)
                    ? ModelLifecycleStatus.Running
                    : ModelLifecycleStatus.NotLoaded;
                if (m.Status != target)
                {
                    m.Status = target;
                }
            }
            if (SelectedModel != null)
            {
                var sel = Models.FirstOrDefault(m => m.Name == SelectedModel.Name);
                if (sel != null && sel.Status != SelectedModel.Status)
                {
                    SelectedModel = sel;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn("Models", $"Refresh status failed: {ex.Message}");
        }
    }

    private async Task ClearStatusAfterDelay()
    {
        await Task.Delay(4000);
        OperationStatus = string.Empty;
    }

    private void BenchmarkSelected()
    {
        if (SelectedModel == null) return;
        System.Windows.MessageBox.Show(
            $"{_loc["Models.Action.Benchmark"]}\n\n{SelectedModel.Name}",
            _loc["Models.Action.Benchmark"],
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void CopyName()
    {
        if (SelectedModel == null) return;
        try
        {
            System.Windows.Clipboard.SetText(SelectedModel.Name);
            OperationStatus = $"{_loc["Models.Action.CopyName"]}: {SelectedModel.Name}";
        }
        catch (Exception ex)
        {
            _log.Warn("Models", $"Copy name failed: {ex.Message}");
        }
    }

    public void QuickStartByIndex(object? indexObj)
    {
        if (indexObj is not int index) return;
        var match = FilteredModels.FirstOrDefault(m => m.ItemNumber == index);
        if (match == null) return;
        SelectedModel = match;
        if (RunModelCommand.CanExecute(null))
        {
            RunModelCommand.Execute(null);
        }
    }

    public async Task PreloadAllAsync()
    {
        var toLoad = Models
            .Where(m => m.Status != ModelLifecycleStatus.Running && m.Status != ModelLifecycleStatus.Loading)
            .Select(m => m.Name)
            .ToList();
        if (toLoad.Count == 0)
        {
            OperationStatus = _loc["Models.ReleaseAllNone"];
            _ = ClearStatusAfterDelay();
            return;
        }

        IsOperating = true;
        OperationStatus = string.Format(_loc["Models.PreloadStarted"], toLoad.Count);
        int success = 0;
        int failed = 0;
        try
        {
            foreach (var name in toLoad)
            {
                try
                {
                    var ok = await _ollamaService.StartModelAsync(name);
                    if (ok) success++;
                    else failed++;
                }
                catch
                {
                    failed++;
                }
            }
        }
        finally
        {
            OperationStatus = string.Format(_loc["Models.PreloadCompleted"], success, failed);
            await RefreshStatusAsync();
            IsOperating = false;
            _ = ClearStatusAfterDelay();
        }
    }

    public string StatusToText(ModelLifecycleStatus status) => status switch
    {
        ModelLifecycleStatus.Running => _loc["Models.Status.Running"],
        ModelLifecycleStatus.Loading => _loc["Models.Status.Loading"],
        ModelLifecycleStatus.NotLoaded => _loc["Models.Status.NotLoaded"],
        ModelLifecycleStatus.Error => _loc["Models.Status.Error"],
        ModelLifecycleStatus.Stopping => _loc["Models.Status.Stopping"],
        ModelLifecycleStatus.Unloading => _loc["Models.Status.Unloading"],
        _ => _loc["Models.Status.Unknown"]
    };
}