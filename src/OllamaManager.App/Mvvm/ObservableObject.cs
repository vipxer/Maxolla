using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OllamaManager.App.Mvvm;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public abstract class AsyncObservableObject : ObservableObject
{
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string? _busyMessage;
    public string? BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    protected async Task RunBusyAsync(string message, Func<Task> action, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        BusyMessage = message;
        try
        {
            await action().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }
}
