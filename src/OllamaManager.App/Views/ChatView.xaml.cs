using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using OllamaManager.App.ViewModels;
using OllamaManager.Core.Models;

namespace OllamaManager.App.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += ChatView_DataContextChanged;
    }

    private void OnThumbnailClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is AttachedImage img)
        {
            var viewer = new ImageViewerWindow(img) { Owner = Window.GetWindow(this) };
            viewer.ShowDialog();
            e.Handled = true;
        }
    }

    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore — user may have clicked an invalid URL
        }
        e.Handled = true;
    }

    private void OnChatContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        if (menu.PlacementTarget is not FrameworkElement fe) return;
        if (fe.DataContext is not ChatTurnViewModel turn) return;

        // Regen / Delete are only meaningful on assistant turns that have finished streaming.
        bool isAssistant = turn.Role == ChatRole.Assistant;
        bool isFinished = turn.IsFinished;
        foreach (var item in menu.Items)
        {
            if (item is MenuItem mi && (mi.Name == "MenuRegenerate" || mi.Name == "MenuDelete"))
            {
                mi.Visibility = (isAssistant && isFinished) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private ChatTurnViewModel? TurnFromMenu(object? sender)
    {
        if (sender is not MenuItem mi) return null;
        if (FindAncestor<ContextMenu>(mi) is { } cm && cm.PlacementTarget is FrameworkElement fe)
        {
            return fe.DataContext as ChatTurnViewModel;
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    private void OnCopyFullClick(object sender, RoutedEventArgs e)
    {
        if (TurnFromMenu(sender) is { } turn && DataContext is ChatViewModel vm)
        {
            vm.CopyTurnCommand.Execute(turn);
        }
    }

    private void OnRegenerateFromMenuClick(object sender, RoutedEventArgs e)
    {
        if (TurnFromMenu(sender) is { } turn && DataContext is ChatViewModel vm)
        {
            vm.RegenerateTurnCommand.Execute(turn);
        }
    }

    private void OnDeleteFromMenuClick(object sender, RoutedEventArgs e)
    {
        if (TurnFromMenu(sender) is { } turn && DataContext is ChatViewModel vm)
        {
            vm.DeleteTurnCommand.Execute(turn);
        }
    }

    private void OnCodeBlockCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string code) return;

        try
        {
            System.Windows.Clipboard.SetText(code);
        }
        catch
        {
            // clipboard occasionally locked by another app — ignore
            return;
        }

        // Briefly flip the label to "✓ 已复制" in green, then revert (ChatGPT-style).
        if (btn.Content is not StackPanel panel) return;
        TextBlock? label = null;
        TextBlock? icon = null;
        foreach (var child in panel.Children)
        {
            if (child is TextBlock tb)
            {
                if (tb.Text == "复制") label = tb;
                else if (tb.Text == "📋") icon = tb;
            }
        }
        if (label == null) return;

        var defaultFg = (System.Windows.Media.Brush?)TryFindResource("TextTertiaryBrush") ?? System.Windows.Media.Brushes.Gray;
        var successFg = (System.Windows.Media.Brush?)TryFindResource("SuccessBrush") ?? System.Windows.Media.Brushes.LimeGreen;
        if (icon != null) icon.Text = "✓";
        label.Text = "已复制";
        label.Foreground = successFg;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        timer.Tick += (_, _) =>
        {
            label.Text = "复制";
            label.Foreground = defaultFg;
            if (icon != null) icon.Text = "📋";
            timer.Stop();
        };
        timer.Start();
    }

    private void OnActionCopyClick(object sender, RoutedEventArgs e)
    {
        // Command runs after this Click handler and writes StatusText="已复制到剪贴板".
        // We add a brief green inline label for instant affordance on the button itself.
        if (sender is not Button btn) return;
        if (btn.FindName("ActionCopyLabel") is not TextBlock label) return;
        FlashActionLabel(btn, label, "已复制", success: true);
    }

    private void OnActionRegenClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.FindName("ActionRegenLabel") is not TextBlock label) return;
        // Turn is about to be removed by the command — no need to revert.
        FlashActionLabel(btn, label, "重新生成中...", success: true, revertAfterMs: 0);
    }

    private void OnActionDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.FindName("ActionDeleteLabel") is not TextBlock label) return;
        // Turn is about to be removed — no need to revert.
        FlashActionLabel(btn, label, "已删除", success: false, revertAfterMs: 0);
    }

    private void FlashActionLabel(Button btn, TextBlock label, string text, bool success, int revertAfterMs = 1500)
    {
        var defaultFg = (System.Windows.Media.Brush?)TryFindResource("TextTertiaryBrush") ?? System.Windows.Media.Brushes.Gray;
        var flashFg = (System.Windows.Media.Brush?)TryFindResource(success ? "SuccessBrush" : "ErrorBrush") ?? (success ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.OrangeRed);
        var original = label.Text;
        var originalFg = label.Foreground ?? defaultFg;

        label.Text = text;
        label.Foreground = flashFg;
        btn.IsEnabled = false;

        if (revertAfterMs <= 0) return;

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(revertAfterMs) };
        timer.Tick += (_, _) =>
        {
            label.Text = original;
            label.Foreground = originalFg;
            btn.IsEnabled = true;
            timer.Stop();
        };
        timer.Start();
    }

    private void ChatView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ChatViewModel oldVm)
        {
            oldVm.ScrollToEndRequested -= ScrollToEnd;
            oldVm.ScrollToEndIfNearBottomRequested -= ScrollToEndIfNearBottom;
        }
        if (e.NewValue is ChatViewModel newVm)
        {
            newVm.ScrollToEndRequested += ScrollToEnd;
            newVm.ScrollToEndIfNearBottomRequested += ScrollToEndIfNearBottom;
        }
    }

    private void ScrollToEnd()
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            ChatScroll.ScrollToEnd();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ScrollToEndIfNearBottom()
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            var sv = ChatScroll;
            var isNearBottom = false;
            if (sv.ExtentHeight > sv.ViewportHeight)
            {
                var distanceFromBottom = sv.ExtentHeight - sv.VerticalOffset - sv.ViewportHeight;
                isNearBottom = distanceFromBottom < 100;
            }
            else
            {
                isNearBottom = true;
            }
            if (isNearBottom)
            {
                ChatScroll.ScrollToEnd();
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (DataContext is ChatViewModel vm && vm.IsGenerating && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}