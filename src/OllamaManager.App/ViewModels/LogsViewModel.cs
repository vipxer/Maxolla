using System.Collections.ObjectModel;
using System.Windows.Input;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;

namespace OllamaManager.App.ViewModels;

public class LogsViewModel : AsyncObservableObject
{
    private readonly ILogService _logService;
    private readonly ILocalizationService _loc;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    private string _filterLevel = "All";
    public string FilterLevel
    {
        get => _filterLevel;
        set
        {
            if (SetProperty(ref _filterLevel, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    public IReadOnlyList<string> AvailableLevels { get; } =
        new[] { "All", "Information", "Warning", "Error", "Critical" };

    public ICommand RefreshCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public LogsViewModel(ILogService logService, ILocalizationService loc)
    {
        _logService = logService;
        _loc = loc;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ClearAllCommand = new AsyncRelayCommand(ClearAllAsync);
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var list = string.IsNullOrWhiteSpace(SearchText)
                ? _logService.GetRecentEntries(500)
                : _logService.SearchEntries(SearchText, 500);

            Entries.Clear();
            foreach (var e in list.OrderByDescending(x => x.Timestamp))
            {
                if (FilterLevel == "All" || e.Level.ToString() == FilterLevel)
                {
                    Entries.Add(e);
                }
            }
        }
        catch (Exception)
        {
            // silent
        }
    }

    private async Task ClearAllAsync()
    {
        try
        {
            await Task.Run(() => _logService.ClearAllLogs());
            await RefreshAsync();
        }
        catch
        {
            // silent
        }
    }

    private void OpenFolder()
    {
        try
        {
            _logService.OpenLogsDirectory();
        }
        catch
        {
            // silent
        }
    }
}