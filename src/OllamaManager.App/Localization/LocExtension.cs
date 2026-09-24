using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using OllamaManager.Core.Interfaces;

namespace OllamaManager.App.Localization;

public class Loc : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public Loc() { }
    public Loc(string key) { Key = key; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key)) return string.Empty;
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationProxy.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}

public class LocConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = parameter as string ?? value as string ?? string.Empty;
        return string.IsNullOrEmpty(key) ? string.Empty : LocalizationProxy.Instance[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public class LocalizationProxy : INotifyPropertyChanged
{
    private static LocalizationProxy? _instance;
    public static LocalizationProxy Instance => _instance ??= new LocalizationProxy();

    private ILocalizationService? _loc;
    private bool _attached;

    public string this[string key]
    {
        get
        {
            EnsureAttached();
            if (_loc == null) return key;
            return _loc[key];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Attach(ILocalizationService loc)
    {
        if (_attached && ReferenceEquals(_loc, loc)) return;
        if (_loc != null)
        {
            _loc.LanguageChanged -= OnLanguageChanged;
        }
        _loc = loc;
        if (_loc != null)
        {
            _loc.LanguageChanged += OnLanguageChanged;
        }
        _attached = true;
        RaiseAllChanged();
    }

    public void RaiseAllChanged()
    {
        var h = PropertyChanged;
        if (h == null) return;
        h(this, new PropertyChangedEventArgs("Item[]"));
        h(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void EnsureAttached()
    {
        if (_attached && _loc != null) return;
        if (Application.Current is App)
        {
            try
            {
                var loc = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService;
                if (loc != null)
                {
                    Attach(loc);
                }
            }
            catch { }
        }
    }

    private void OnLanguageChanged(object? sender, CultureInfo e)
    {
        RaiseAllChanged();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool v = value switch
        {
            bool b => b,
            int i => i > 0,
            long l => l > 0,
            double d => d > 0,
            string s => !string.IsNullOrEmpty(s),
            null => false,
            _ => true
        };
        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase)) v = !v;
        return v ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility vis && vis == Visibility.Visible;
}

public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool v = value != null && !(value is string str && string.IsNullOrEmpty(str));
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase)) v = !v;
        if (targetType == typeof(System.Windows.Visibility))
        {
            return v ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return v;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class CountToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n > 0;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class IndexOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int offset = 1;
        if (parameter is int i) offset = i;
        else if (parameter is string s && int.TryParse(s, out var p)) offset = p;
        return value is int n ? n + offset : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class MarkdownToInlinesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var text = values.Length > 0 ? values[0] as string ?? string.Empty : string.Empty;
        var host = values.Length > 1 ? values[1] as System.Windows.Documents.InlineCollection : null;
        if (host == null) return Binding.DoNothing;

        host.Clear();
        ParseInto(text, host);
        return Binding.DoNothing;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();

    private static void ParseInto(string text, System.Windows.Documents.InlineCollection host)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        bool firstLine = true;
        foreach (var rawLine in lines)
        {
            if (!firstLine)
            {
                host.Add(new System.Windows.Documents.LineBreak());
            }
            firstLine = false;
            ParseLine(rawLine, host);
        }
    }

    private static void ParseLine(string line, System.Windows.Documents.InlineCollection host)
    {
        int i = 0;
        while (i < line.Length)
        {
            if (i + 1 < line.Length && line[i] == '*' && line[i + 1] == '*')
            {
                int end = line.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    host.Add(new System.Windows.Documents.Run(line.Substring(i + 2, end - i - 2)) { FontWeight = FontWeights.Bold });
                    i = end + 2;
                    continue;
                }
            }
            if (line[i] == '*')
            {
                int end = line.IndexOf('*', i + 1);
                if (end > i + 1)
                {
                    host.Add(new System.Windows.Documents.Run(line.Substring(i + 1, end - i - 1)) { FontStyle = FontStyles.Italic });
                    i = end + 1;
                    continue;
                }
            }
            if (line[i] == '`')
            {
                int end = line.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    var code = line.Substring(i + 1, end - i - 1);
                    var run = new System.Windows.Documents.Run(code)
                    {
                        FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 128, 128, 128))
                    };
                    host.Add(run);
                    i = end + 1;
                    continue;
                }
            }
            int next = line.Length;
            for (int j = i + 1; j < line.Length; j++)
            {
                if ((line[j] == '*' || line[j] == '`') &&
                    !(j + 1 < line.Length && line[j] == '*' && line[j + 1] == '*' && (line[i] == '*' && line[i + 1] == '*')))
                {
                    next = j;
                    break;
                }
            }
            host.Add(new System.Windows.Documents.Run(line.Substring(i, next - i)));
            i = next;
        }
    }
}
