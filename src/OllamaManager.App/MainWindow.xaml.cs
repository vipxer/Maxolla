using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using OllamaManager.App.Mvvm;
using OllamaManager.App.ViewModels;
using OllamaManager.App.Views;
using OllamaManager.Core.Interfaces;

namespace OllamaManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILocalizationService _loc;
    private readonly IDiagnosticsService _diagnostics;
    private readonly DispatcherTimer _clockTimer;
    private bool _exitRequested;

    public MainWindow(MainViewModel viewModel, ILocalizationService loc, IDiagnosticsService diagnostics)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _loc = loc;
        _diagnostics = diagnostics;
        DataContext = viewModel;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        _loc.LanguageChanged += OnLanguageChanged;
        ApplyFlowDirection(_loc.IsRtl);

        _viewModel.PropertyChanged += (_, ev) =>
        {
            if (ev.PropertyName == nameof(MainViewModel.OllamaStatusKind))
            {
                Dispatcher.Invoke(RefreshTrayStatus);
            }
        };

        _viewModel.RunningModels.CollectionChanged += (_, _) =>
        {
            Dispatcher.Invoke(RefreshTrayRunningModels);
        };

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => _viewModel.LastUpdated = DateTime.Now;
        _clockTimer.Start();

        try
        {
            TrayIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute));
        }
        catch
        {
            try
            {
                var decoder = System.Windows.Media.Imaging.IconBitmapDecoder.Create(
                    new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute),
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                TrayIcon.IconSource = decoder.Frames[0];
            }
            catch { }
        }
    }

    private void OnLanguageChanged(object? sender, System.Globalization.CultureInfo e)
    {
        Dispatcher.Invoke(() =>
        {
            ApplyFlowDirection(_loc.IsRtl);
            OllamaManager.App.Localization.LocalizationProxy.Instance.RaiseAllChanged();
        });
    }

    private void ApplyFlowDirection(bool isRtl)
    {
        FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        _viewModel.NavigateTo("Dashboard");
        RefreshTrayStatus();
        RefreshTrayRunningModels();
        _ = RunStartupDiagnosticAsync();
    }

    private async Task RunStartupDiagnosticAsync()
    {
        try
        {
            var report = await _diagnostics.RunFullDiagnosticAsync();
            if (!report.HasErrors && !report.HasWarnings) return;

            var summary = $"{report.OkCount} OK · {report.WarningCount} {GetSeverityShort("warning")} · {report.ErrorCount} {GetSeverityShort("error")}";
            var title = _loc["Nav.Diagnostics"];
            var message = report.HasErrors
                ? $"{_loc["Tray.Status.NotInstalled"]}\n{summary}"
                : summary;
            var icon = report.HasErrors ? BalloonIcon.Error : BalloonIcon.Warning;
            Dispatcher.Invoke(() =>
            {
                try { TrayIcon.ShowBalloonTip(title, message, icon); } catch { }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Startup diagnostic failed: {ex.Message}");
        }
    }

    private string GetSeverityShort(string kind) => kind switch
    {
        "error" => "❌",
        "warning" => "⚠",
        _ => "·"
    };

    private void OnNavItemChecked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb && rb.DataContext is NavItem item)
        {
            _viewModel.NavigateTo(item.Key);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            try { TrayIcon?.Dispose(); } catch { }
            return;
        }
        e.Cancel = true;
        Hide();
        if (WindowState == WindowState.Normal)
        {
            WindowState = WindowState.Minimized;
        }
        ShowMinimizedBalloon();
    }

    private void ShowMinimizedBalloon()
    {
        try
        {
            var title = _loc["Tray.MinimizedTitle"];
            var message = _loc["Tray.MinimizedMessage"];
            TrayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
        }
        catch { }
    }

    private void OnTrayIconDoubleClick(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void OnTrayRightClick(object sender, RoutedEventArgs e)
    {
        // Pop up our themed WPF ContextMenu (Hardcodet.NotifyIcon's built-in ContextMenu
        // renders as a native Win32 HMENU and ignores WPF styles, causing scrollbars and
        // clipped text on HiDPI displays).
        if (TryFindResource("TrayMenu") is not System.Windows.Controls.ContextMenu menu) return;

        // Refresh the status header and the running-models list right before showing
        RefreshTrayStatus();
        RefreshTrayRunningModels();

        // Position near the cursor (bottom-right of the primary work area, like real tray menus)
        var pt = GetCursorPos();
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Absolute;
        menu.HorizontalOffset = pt.X - 8;
        menu.VerticalOffset = pt.Y - 8;
        menu.IsOpen = true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
    private static System.Drawing.Point GetCursorPos()
    {
        GetCursorPos(out var p);
        return new System.Drawing.Point(p.x, p.y);
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    private void OnTrayMenuOpen(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void OnTrayMenuNavigate(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is string key)
        {
            RestoreFromTray();
            _viewModel.NavigateTo(key);
        }
    }

    public void RefreshTrayStatus()
    {
        if (TryFindResource("TrayMenu") is not System.Windows.Controls.ContextMenu menu) return;
        if (menu.Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault() is not { } statusItem) return;
        // Switch is keyed on the *kind* (English constant), not the localized text
        var (dot, key) = _viewModel.OllamaStatusKind switch
        {
            "Running" => ("🟢", "Tray.Status.Running"),
            "NotRunning" => ("🟡", "Tray.Status.NotRunning"),
            "NotInstalled" => ("🔴", "Tray.Status.NotInstalled"),
            _ => ("⚪", "Status.Unknown")
        };
        statusItem.Header = $"{dot}  状态：{_loc[key]}";
    }

    public void RefreshTrayRunningModels()
    {
        if (TryFindResource("TrayMenu") is not System.Windows.Controls.ContextMenu menu) return;

        System.Windows.Controls.Separator? pre = null, post = null;
        System.Windows.Controls.MenuItem? headerItem = null, emptyItem = null;
        foreach (var item in menu.Items)
        {
            if (item is System.Windows.Controls.Separator s)
            {
                if (s.Name == "TrayRunningPre") pre = s;
                else if (s.Name == "TrayRunningPost") post = s;
            }
            else if (item is System.Windows.Controls.MenuItem mi)
            {
                if (mi.Name == "TrayRunningHeader") headerItem = mi;
                else if (mi.Name == "TrayRunningEmpty") emptyItem = mi;
            }
        }
        if (pre == null || post == null || headerItem == null || emptyItem == null) return;

        // Remove dynamically-added model items between the two separators, but keep the
        // "no models running" placeholder so we can toggle its visibility.
        int preIdx = menu.Items.IndexOf(pre);
        int postIdx = menu.Items.IndexOf(post);
        for (int i = postIdx - 1; i > preIdx; i--)
        {
            if (ReferenceEquals(menu.Items[i], emptyItem)) continue;
            menu.Items.RemoveAt(i);
        }

        var models = _viewModel.RunningModels;
        int count = models.Count;
        string headerText = _loc["Tray.RunningModels"];
        headerItem.Header = count > 0 ? $"{headerText} ({count})" : headerText;

        if (count == 0)
        {
            emptyItem.Header = _loc["Tray.RunningModelsEmpty"];
            emptyItem.Visibility = Visibility.Visible;
            return;
        }

        emptyItem.Visibility = Visibility.Collapsed;

        // Insert one MenuItem per running model right before the post separator.
        postIdx = menu.Items.IndexOf(post);
        int insertIdx = postIdx;
        foreach (var m in models)
        {
            var name = string.IsNullOrWhiteSpace(m.Name) ? _loc["Tray.RunningModelsEmpty"] : m.Name;
            var mi = new System.Windows.Controls.MenuItem
            {
                Header = $"🟢  {name}",
                Tag = "Running",
                MinWidth = 200
            };
            mi.Click += OnTrayMenuNavigate;
            menu.Items.Insert(insertIdx++, mi);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void OnTrayMenuExit(object sender, RoutedEventArgs e)
    {
        _exitRequested = true;
        Close();
    }

    private void OnNavExitClick(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            _loc["Tray.ExitConfirm"],
            _loc["Nav.Exit"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        _exitRequested = true;
        Close();
    }

    private void OnBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
    }
}

public class NavItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _label = string.Empty;
    private bool _isSelected;
    public string Key { get; set; } = string.Empty;
    public string Label
    {
        get => _label;
        set
        {
            if (_label == value) return;
            _label = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Label)));
        }
    }
    public string Glyph { get; set; } = string.Empty;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
