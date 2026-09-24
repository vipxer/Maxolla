using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class RunningViewModel : AsyncObservableObject
{
    private readonly IOllamaService _ollamaService;
    private readonly IModelService _modelService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public ObservableCollection<RunningModelInfo> RunningModels { get; } = new();

    private RunningModelInfo? _selectedModel;
    public RunningModelInfo? SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    private string _operationStatus = string.Empty;
    public string OperationStatus
    {
        get => _operationStatus;
        set => SetProperty(ref _operationStatus, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand UnloadCommand { get; }
    public ICommand ReleaseAllCommand { get; }
    public ICommand StartCommand { get; }

    public RunningViewModel(
        IOllamaService ollamaService,
        IModelService modelService,
        ILocalizationService loc,
        ILogService log)
    {
        _ollamaService = ollamaService;
        _modelService = modelService;
        _loc = loc;
        _log = log;

        LoadCommand = new AsyncRelayCommand(LoadRunningModelsAsync);
        UnloadCommand = new AsyncRelayCommand(UnloadSelectedAsync, () => SelectedModel != null);
        ReleaseAllCommand = new AsyncRelayCommand(ReleaseAllAsync, () => RunningModels.Count > 0);
        StartCommand = new AsyncRelayCommand(StartSelectedAsync, () => SelectedModel != null);
    }

    public async Task InitializeAsync()
    {
        await LoadRunningModelsAsync();
    }

    public async Task LoadRunningModelsAsync()
    {
        try
        {
            var status = await _ollamaService.GetCurrentStatusAsync();
            if (!status.IsApiAvailable)
            {
                RunningModels.Clear();
                return;
            }
            var list = await _ollamaService.GetRunningModelsAsync();
            RunningModels.Clear();
            foreach (var r in list) RunningModels.Add(r);
        }
        catch (Exception ex)
        {
            _log.Error("Running", $"Load failed: {ex.Message}", ex);
        }
    }

    private async Task UnloadSelectedAsync()
    {
        if (SelectedModel == null) return;
        var name = SelectedModel.Name;
        OperationStatus = $"{_loc["Models.Unloading"]} {name}...";
        try
        {
            var ok = await _modelService.UnloadModelAsync(name);
            OperationStatus = ok
                ? $"{name}: {_loc["Models.UnloadSucceeded"]}"
                : $"{name}: {_loc["Models.UnloadFailed"]}";
        }
        catch (Exception ex)
        {
            OperationStatus = $"{name}: {_loc["Models.UnloadFailed"]} ({ex.Message})";
            _log.Error("Running", $"Unload failed: {ex.Message}", ex);
        }
        await LoadRunningModelsAsync();
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
        OperationStatus = _loc["Models.ReleasingAll"] + "...";
        try
        {
            var released = await _modelService.ReleaseAllRunningModelsAsync();
            var fmt = _loc["Running.ReleasedCount"].Replace("{0}", released.ToString());
            OperationStatus = fmt;
            _log.Info("Running", $"Released {released} models");
        }
        catch (Exception ex)
        {
            OperationStatus = $"{_loc["Models.ReleaseAll"]} {_loc["Models.Failed"]} ({ex.Message})";
            _log.Error("Running", $"Release all failed: {ex.Message}", ex);
        }
        await LoadRunningModelsAsync();
        _ = ClearStatusAfterDelay();
    }

    private async Task StartSelectedAsync()
    {
        if (SelectedModel == null) return;
        var name = SelectedModel.Name;
        OperationStatus = $"{_loc["Models.LoadingHint"]} ({name})";
        try
        {
            var ok = await _ollamaService.StartModelAsync(name);
            OperationStatus = ok
                ? $"{name}: {_loc["Models.RunSucceeded"]}"
                : $"{name}: {_loc["Models.RunFailed"]}";
        }
        catch (Exception ex)
        {
            OperationStatus = $"{name}: {_loc["Models.RunFailed"]} ({ex.Message})";
            _log.Error("Running", $"Start failed: {ex.Message}", ex);
        }
        await LoadRunningModelsAsync();
        _ = ClearStatusAfterDelay();
    }

    private async Task ClearStatusAfterDelay()
    {
        await Task.Delay(4000);
        OperationStatus = string.Empty;
    }
}