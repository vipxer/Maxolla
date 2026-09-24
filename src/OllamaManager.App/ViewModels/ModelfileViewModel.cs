using System.Collections.ObjectModel;
using System.Windows.Input;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public class ModelfileViewModel : AsyncObservableObject
{
    private readonly IModelfileService _modelfileService;
    private readonly IModelService _modelService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    private string _modelfileText = string.Empty;
    public string ModelfileText
    {
        get => _modelfileText;
        set
        {
            if (SetProperty(ref _modelfileText, value))
            {
                _ = ValidateAsync();
            }
        }
    }

    private string _newModelName = string.Empty;
    public string NewModelName
    {
        get => _newModelName;
        set => SetProperty(ref _newModelName, value);
    }

    private string _fromModel = "llama3";
    public string FromModel
    {
        get => _fromModel;
        set => SetProperty(ref _fromModel, value);
    }

    private string _systemPrompt = string.Empty;
    public string SystemPrompt
    {
        get => _systemPrompt;
        set => SetProperty(ref _systemPrompt, value);
    }

    private string _template = string.Empty;
    public string Template
    {
        get => _template;
        set => SetProperty(ref _template, value);
    }

    public ObservableCollection<ModelfileParameter> AvailableParameters { get; } = new();
    public ObservableCollection<string> ValidationMessages { get; } = new();

    public ICommand ValidateCommand { get; }
    public ICommand CreateModelCommand { get; }
    public ICommand GenerateFromParametersCommand { get; }
    public ICommand AddParameterCommand { get; }
    public ICommand LoadFromFileCommand { get; }
    public ICommand SaveToFileCommand { get; }

    public ModelfileViewModel(
        IModelfileService modelfileService,
        IModelService modelService,
        ILocalizationService loc,
        ILogService log)
    {
        _modelfileService = modelfileService;
        _modelService = modelService;
        _loc = loc;
        _log = log;

        ValidateCommand = new AsyncRelayCommand(ValidateAsync);
        CreateModelCommand = new AsyncRelayCommand(CreateModelAsync,
            () => !string.IsNullOrWhiteSpace(NewModelName) && !IsBusy);
        GenerateFromParametersCommand = new AsyncRelayCommand(GenerateFromParametersAsync);
        AddParameterCommand = new RelayCommand(AddParameter);
        LoadFromFileCommand = new AsyncRelayCommand(LoadFromFileAsync);
        SaveToFileCommand = new AsyncRelayCommand(SaveToFileAsync, () => !string.IsNullOrEmpty(ModelfileText));

        _ = LoadParametersAsync();
    }

    private async Task LoadParametersAsync()
    {
        try
        {
            var list = await _modelfileService.GetAvailableParametersAsync();
            AvailableParameters.Clear();
            foreach (var p in list) AvailableParameters.Add(p);
        }
        catch (Exception ex)
        {
            _log.Warn("Modelfile", $"Load parameters failed: {ex.Message}");
        }
    }

    public async Task ValidateAsync()
    {
        try
        {
            var result = await _modelfileService.ValidateAsync(ModelfileText);
            ValidationMessages.Clear();
            if (result.Errors != null)
                foreach (var msg in result.Errors) ValidationMessages.Add(msg);
            if (result.Warnings != null)
                foreach (var msg in result.Warnings) ValidationMessages.Add(msg);
        }
        catch (Exception ex)
        {
            _log.Warn("Modelfile", $"Validation error: {ex.Message}");
        }
    }

    private async Task CreateModelAsync()
    {
        if (string.IsNullOrWhiteSpace(NewModelName) || string.IsNullOrWhiteSpace(ModelfileText))
            return;

        await RunBusyAsync(_loc["Modelfile.Creating"], async () =>
        {
            try
            {
                var ok = await _modelfileService.CreateModelAsync(NewModelName.Trim(), ModelfileText);
                if (ok)
                {
                    _ = _modelService.GetModelsAsync();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Modelfile", $"Create model failed: {ex.Message}", ex);
            }
        });
    }

    private async Task GenerateFromParametersAsync()
    {
        try
        {
            var parameters = new Dictionary<string, object>();
            ModelfileText = await _modelfileService.GenerateFromParametersAsync(
                FromModel, parameters, string.IsNullOrEmpty(SystemPrompt) ? null : SystemPrompt,
                string.IsNullOrEmpty(Template) ? null : Template);
            await ValidateAsync();
        }
        catch (Exception ex)
        {
            _log.Error("Modelfile", $"Generate failed: {ex.Message}", ex);
        }
    }

    private void AddParameter(object? parameter)
    {
        if (parameter is not ModelfileParameter p) return;
        var line = $"{p.Key} {p.Value}\n";
        ModelfileText += line;
    }

    private async Task LoadFromFileAsync()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Modelfile|*.*",
                Title = _loc["Modelfile.OpenTitle"]
            };
            if (dlg.ShowDialog() == true)
            {
                var content = await _modelfileService.ReadModelfileFromDiskAsync(dlg.FileName);
                if (content != null) ModelfileText = content;
            }
        }
        catch (Exception ex)
        {
            _log.Error("Modelfile", $"Open failed: {ex.Message}", ex);
        }
    }

    private async Task SaveToFileAsync()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Modelfile|*.*",
                Title = _loc["Modelfile.SaveTitle"],
                FileName = "Modelfile"
            };
            if (dlg.ShowDialog() == true)
            {
                await _modelfileService.WriteModelfileToDiskAsync(dlg.FileName, ModelfileText);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Modelfile", $"Save failed: {ex.Message}", ex);
        }
    }
}