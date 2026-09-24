using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OllamaManager.App.Mvvm;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.App.ViewModels;

public enum MarkdownKind
{
    Text,
    Bold,
    Italic,
    InlineCode,
    CodeBlock,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    BulletList,
    NumberedList,
    Quote,
    HorizontalRule,
    Link
}

public sealed class MarkdownSegment
{
    public MarkdownKind Kind { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Url { get; init; }
    public IReadOnlyList<MarkdownSegment>? Children { get; init; }
    public string? Language { get; init; }
}

public class ChatTurnViewModel : ObservableObject
{
    public ChatRole Role { get; }

    public ChatTurnViewModel(ChatRole role, string content = "")
    {
        Role = role;
        _content = content ?? string.Empty;
        if (!string.IsNullOrEmpty(_content))
        {
            RebuildSegments();
        }
    }

    public ChatRole RoleEnum => Role;

    public ObservableCollection<AttachedImage> Images { get; } = new();

    public bool HasImages => Images.Count > 0;

    private string _content = string.Empty;
    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value ?? string.Empty))
            {
                if (!_isStreaming)
                {
                    RebuildSegments();
                }
            }
        }
    }

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            if (SetProperty(ref _isStreaming, value))
            {
                OnPropertyChanged(nameof(IsFinished));
                if (!value)
                {
                    RebuildSegments();
                }
            }
        }
    }

    private bool _isThinking;
    public bool IsThinking
    {
        get => _isThinking;
        set => SetProperty(ref _isThinking, value);
    }

    public bool IsFinished => !_isStreaming && !_isThinking;

    private string _stats = string.Empty;
    public string Stats
    {
        get => _stats;
        set => SetProperty(ref _stats, value ?? string.Empty);
    }

    public ObservableCollection<MarkdownSegment> Segments { get; } = new();

    private void RebuildSegments()
    {
        Segments.Clear();
        foreach (var s in MarkdownSegmentBuilder.Parse(_content))
        {
            Segments.Add(s);
        }
    }
}

internal static class MarkdownSegmentBuilder
{
    public static System.Collections.Generic.IEnumerable<MarkdownSegment> Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var normalized = text.Replace("\r\n", "\n");
        var blocks = ParseBlocks(normalized);
        foreach (var block in blocks)
        {
            yield return block;
        }
    }

    private static System.Collections.Generic.IEnumerable<MarkdownSegment> ParseBlocks(string text)
    {
        var lines = text.Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // Code block: ``` optional language
            if (line.StartsWith("```"))
            {
                var lang = line.Length > 3 ? line.Substring(3).Trim() : null;
                var sb = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].StartsWith("```"))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(lines[i]);
                    i++;
                }
                if (i < lines.Length) i++; // skip closing ```
                yield return new MarkdownSegment { Kind = MarkdownKind.CodeBlock, Text = sb.ToString(), Language = lang };
                continue;
            }

            // Horizontal rule: --- or *** or ___
            if (IsHorizontalRule(line))
            {
                yield return new MarkdownSegment { Kind = MarkdownKind.HorizontalRule, Text = string.Empty };
                i++;
                continue;
            }

            // Headings: # ... ######
            if (line.Length > 0 && line[0] == '#')
            {
                int level = 0;
                while (level < line.Length && line[level] == '#' && level < 4) level++;
                if (level > 0 && (level == line.Length || line[level] == ' '))
                {
                    var content = line.Substring(level).Trim();
                    var kind = level switch
                    {
                        1 => MarkdownKind.Heading1,
                        2 => MarkdownKind.Heading2,
                        3 => MarkdownKind.Heading3,
                        _ => MarkdownKind.Heading4
                    };
                    yield return new MarkdownSegment { Kind = kind, Text = content };
                    i++;
                    continue;
                }
            }

            // Block quote: > ...
            if (line.StartsWith(">"))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].StartsWith(">"))
                {
                    quoteLines.Add(lines[i].Substring(1).TrimStart());
                    i++;
                }
                var quoteText = string.Join("\n", quoteLines);
                yield return new MarkdownSegment { Kind = MarkdownKind.Quote, Text = quoteText };
                continue;
            }

            // Bullet list: - or * at start of line
            if (IsBulletLine(line))
            {
                var items = new List<MarkdownSegment>();
                while (i < lines.Length && IsBulletLine(lines[i]))
                {
                    var itemText = StripBulletPrefix(lines[i]).Trim();
                    items.Add(new MarkdownSegment { Kind = MarkdownKind.Text, Text = itemText });
                    i++;
                }
                yield return new MarkdownSegment
                {
                    Kind = MarkdownKind.BulletList,
                    Text = string.Empty,
                    Children = items
                };
                continue;
            }

            // Numbered list: 1. 2. etc.
            if (IsNumberedLine(line))
            {
                var items = new List<MarkdownSegment>();
                while (i < lines.Length && IsNumberedLine(lines[i]))
                {
                    var itemText = StripNumberedPrefix(lines[i]).Trim();
                    items.Add(new MarkdownSegment { Kind = MarkdownKind.Text, Text = itemText });
                    i++;
                }
                yield return new MarkdownSegment
                {
                    Kind = MarkdownKind.NumberedList,
                    Text = string.Empty,
                    Children = items
                };
                continue;
            }

            // Blank line: skip
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Plain paragraph: collect consecutive non-blank, non-special lines into one block.
            var paragraph = new StringBuilder();
            paragraph.Append(line);
            i++;
            while (i < lines.Length
                && !string.IsNullOrWhiteSpace(lines[i])
                && !lines[i].StartsWith("```")
                && !IsHorizontalRule(lines[i])
                && !IsBulletLine(lines[i])
                && !IsNumberedLine(lines[i])
                && !lines[i].StartsWith(">")
                && !(lines[i].Length > 0 && lines[i][0] == '#'))
            {
                paragraph.Append('\n');
                paragraph.Append(lines[i]);
                i++;
            }
            foreach (var inline in ParseInline(paragraph.ToString()))
            {
                yield return inline;
            }
        }
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3) return false;
        char c = trimmed[0];
        if (c != '-' && c != '*' && c != '_') return false;
        return trimmed.All(ch => ch == c);
    }

    private static bool IsBulletLine(string line)
    {
        var t = line.TrimStart();
        if (t.Length < 2) return false;
        if (t[0] != '-' && t[0] != '*') return false;
        return t[1] == ' ';
    }

    private static string StripBulletPrefix(string line)
    {
        var t = line.TrimStart();
        return t.Substring(1).TrimStart();
    }

    private static bool IsNumberedLine(string line)
    {
        var t = line.TrimStart();
        int i = 0;
        while (i < t.Length && char.IsDigit(t[i])) i++;
        if (i == 0 || i > 3) return false;
        if (i >= t.Length) return false;
        return t[i] == '.';
    }

    private static string StripNumberedPrefix(string line)
    {
        var t = line.TrimStart();
        int i = 0;
        while (i < t.Length && char.IsDigit(t[i])) i++;
        return t.Substring(i + 1).TrimStart();
    }

    private static System.Collections.Generic.IEnumerable<MarkdownSegment> ParseInline(string text)
    {
        // Strip raw HTML tags that some models emit
        var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", string.Empty);

        int i = 0;
        var buf = new StringBuilder();
        while (i < cleaned.Length)
        {
            // Inline code: `...`
            if (cleaned[i] == '`')
            {
                int end = cleaned.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    if (buf.Length > 0)
                    {
                        yield return new MarkdownSegment { Kind = MarkdownKind.Text, Text = buf.ToString() };
                        buf.Clear();
                    }
                    yield return new MarkdownSegment { Kind = MarkdownKind.InlineCode, Text = cleaned.Substring(i + 1, end - i - 1) };
                    i = end + 1;
                    continue;
                }
            }

            // Bold: **...**
            if (i + 1 < cleaned.Length && cleaned[i] == '*' && cleaned[i + 1] == '*')
            {
                int end = cleaned.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    if (buf.Length > 0)
                    {
                        yield return new MarkdownSegment { Kind = MarkdownKind.Text, Text = buf.ToString() };
                        buf.Clear();
                    }
                    var inner = cleaned.Substring(i + 2, end - i - 2);
                    yield return new MarkdownSegment { Kind = MarkdownKind.Bold, Text = inner };
                    i = end + 2;
                    continue;
                }
            }

            // Italic: *...*
            if (cleaned[i] == '*')
            {
                int end = cleaned.IndexOf('*', i + 1);
                if (end > i + 1)
                {
                    if (buf.Length > 0)
                    {
                        yield return new MarkdownSegment { Kind = MarkdownKind.Text, Text = buf.ToString() };
                        buf.Clear();
                    }
                    var inner = cleaned.Substring(i + 1, end - i - 1);
                    yield return new MarkdownSegment { Kind = MarkdownKind.Italic, Text = inner };
                    i = end + 1;
                    continue;
                }
            }

            // Link: [text](url)
            if (cleaned[i] == '[')
            {
                int closeBracket = cleaned.IndexOf(']', i + 1);
                if (closeBracket > i + 1 && closeBracket + 1 < cleaned.Length && cleaned[closeBracket + 1] == '(')
                {
                    int closeParen = cleaned.IndexOf(')', closeBracket + 2);
                    if (closeParen > closeBracket + 2)
                    {
                        if (buf.Length > 0)
                        {
                            yield return new MarkdownSegment { Kind = MarkdownKind.Text, Text = buf.ToString() };
                            buf.Clear();
                        }
                        var label = cleaned.Substring(i + 1, closeBracket - i - 1);
                        var url = cleaned.Substring(closeBracket + 2, closeParen - closeBracket - 2);
                        yield return new MarkdownSegment { Kind = MarkdownKind.Link, Text = label, Url = url };
                        i = closeParen + 1;
                        continue;
                    }
                }
            }

            buf.Append(cleaned[i]);
            i++;
        }
        if (buf.Length > 0)
        {
            yield return new MarkdownSegment { Kind = MarkdownKind.Text, Text = buf.ToString() };
        }
    }
}

public sealed class AttachedImage
{
    public string Path { get; set; } = string.Empty;
    public string Base64 { get; set; } = string.Empty;
    public BitmapImage? Thumbnail { get; set; }
    public long OriginalSize { get; set; }
    public int ScaledWidth { get; set; }
    public int ScaledHeight { get; set; }

    public string DisplaySize
    {
        get
        {
            if (ScaledWidth > 0 && ScaledHeight > 0)
            {
                return $"{ScaledWidth}×{ScaledHeight}";
            }
            if (OriginalSize > 0)
            {
                return FormatBytes(OriginalSize);
            }
            return string.Empty;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }
}

public class ChatViewModel : AsyncObservableObject
{
    private readonly IOllamaService _ollamaService;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public ObservableCollection<ChatTurnViewModel> Turns { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<string> ModelCapabilities { get; } = new();
    public ObservableCollection<AttachedImage> AttachedImages { get; } = new();

    public event Action? ScrollToEndRequested;
    public event Action? ScrollToEndIfNearBottomRequested;

    private string _selectedModel = string.Empty;
    public string SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value ?? string.Empty) && !string.IsNullOrWhiteSpace(value))
            {
                _ = ProbeCapabilitiesAsync(value);
            }
        }
    }

    private string _draft = string.Empty;
    public string Draft
    {
        get => _draft;
        set
        {
            if (SetProperty(ref _draft, value ?? string.Empty))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private bool _isGenerating;
    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIdle => !_isGenerating;

    private bool _isThinking;
    public bool IsThinking
    {
        get => _isThinking;
        set => SetProperty(ref _isThinking, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    private bool _enableStream = true;
    public bool EnableStream
    {
        get => _enableStream;
        set => SetProperty(ref _enableStream, value);
    }

    private bool _enableThinking;
    public bool EnableThinking
    {
        get => _enableThinking;
        set => SetProperty(ref _enableThinking, value);
    }

    private bool _enableVision;
    public bool EnableVision
    {
        get => _enableVision;
        set => SetProperty(ref _enableVision, value);
    }

    private bool _isProbingCapabilities;
    public bool IsProbingCapabilities
    {
        get => _isProbingCapabilities;
        set => SetProperty(ref _isProbingCapabilities, value);
    }

    public bool IsVisionCapable => ModelCapabilities.Contains("vision");
    public bool IsThinkingCapable => ModelCapabilities.Contains("thinking");
    public bool SupportsTools => ModelCapabilities.Contains("tools");
    public bool IsChatEmpty => Turns.Count == 0;
    public bool HasAnyCapability => ModelCapabilities.Count > 0;
    public bool HasAttachedImages => AttachedImages.Count > 0;

    private double _firstTokenMs;
    public double FirstTokenMs
    {
        get => _firstTokenMs;
        set => SetProperty(ref _firstTokenMs, value);
    }

    private double _totalTps;
    public double TotalTps
    {
        get => _totalTps;
        set => SetProperty(ref _totalTps, value);
    }

    private int _evalCount;
    public int EvalCount
    {
        get => _evalCount;
        set => SetProperty(ref _evalCount, value);
    }

    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, List<string>> _capabilityCache = new(StringComparer.OrdinalIgnoreCase);

    public ICommand SendCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ReloadModelsCommand { get; }
    public ICommand AttachImageCommand { get; }
    public ICommand RemoveImageCommand { get; }
    public ICommand CopyTurnCommand { get; }
    public ICommand RegenerateTurnCommand { get; }
    public ICommand DeleteTurnCommand { get; }

    public ChatViewModel(IOllamaService ollamaService, ILocalizationService loc, ILogService log)
    {
        _ollamaService = ollamaService;
        _loc = loc;
        _log = log;

        SendCommand = new AsyncRelayCommand(SendAsync,
            () => !IsGenerating && !string.IsNullOrWhiteSpace(Draft) && !string.IsNullOrWhiteSpace(SelectedModel));
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsGenerating);
        ClearCommand = new AsyncRelayCommand(ClearAsync);
        ReloadModelsCommand = new AsyncRelayCommand(LoadModelsAsync);
        AttachImageCommand = new AsyncRelayCommand(AttachImageAsync, () => IsVisionCapable && !IsGenerating);
        RemoveImageCommand = new RelayCommand(p => { if (p is AttachedImage img) AttachedImages.Remove(img); });
        CopyTurnCommand = new RelayCommand(CopyTurn, p => p is ChatTurnViewModel t && !string.IsNullOrEmpty(t.Content));
        RegenerateTurnCommand = new AsyncRelayCommand(RegenerateTurnAsync,
            p => p is ChatTurnViewModel t && t.Role == ChatRole.Assistant && t.IsFinished && !IsGenerating);
        DeleteTurnCommand = new RelayCommand(DeleteTurn, p => p is ChatTurnViewModel t && !IsGenerating);

        Turns.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsChatEmpty));
        };

        ModelCapabilities.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsVisionCapable));
            OnPropertyChanged(nameof(IsThinkingCapable));
            OnPropertyChanged(nameof(SupportsTools));
            OnPropertyChanged(nameof(HasAnyCapability));
            CommandManager.InvalidateRequerySuggested();
        };

        AttachedImages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAttachedImages));
        };
    }

    public async Task InitializeAsync()
    {
        await LoadModelsAsync();
    }

    public async Task LoadModelsAsync()
    {
        try
        {
            var models = await _ollamaService.GetModelsAsync();
            AvailableModels.Clear();
            foreach (var m in models)
            {
                AvailableModels.Add(m.Name);
            }
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
            _log.Warn("Chat", $"Load models failed: {ex.Message}");
        }
    }

    public void SetModel(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        if (!AvailableModels.Contains(modelName))
        {
            AvailableModels.Add(modelName);
        }
        SelectedModel = modelName;
    }

    private async Task ProbeCapabilitiesAsync(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        if (_capabilityCache.TryGetValue(modelName, out var cached))
        {
            ApplyCapabilities(cached);
            return;
        }

        IsProbingCapabilities = true;
        try
        {
            var details = await _ollamaService.GetModelDetailsAsync(modelName);
            var caps = details?.Capabilities ?? new List<string>();
            _capabilityCache[modelName] = caps;
            ApplyCapabilities(caps);
        }
        catch (Exception ex)
        {
            _log.Warn("Chat", $"Probe capabilities failed for {modelName}: {ex.Message}");
            ApplyCapabilities(new List<string>());
        }
        finally
        {
            IsProbingCapabilities = false;
        }
    }

    private void ApplyCapabilities(List<string> caps)
    {
        ModelCapabilities.Clear();
        foreach (var c in caps)
        {
            ModelCapabilities.Add(c);
        }
        // Auto-enable toggles for capabilities the model actually supports.
        // Stream is universally supported by Ollama models, so enable it by default.
        EnableStream = true;
        if (IsThinkingCapable) EnableThinking = true;
        if (IsVisionCapable) EnableVision = true;
        // Defensive: if for some reason the cache contains a model that later lost a capability,
        // don't leave a stale toggle stuck on.
        if (!IsThinkingCapable && EnableThinking) EnableThinking = false;
        if (!IsVisionCapable && EnableVision) EnableVision = false;
    }

    private async Task AttachImageAsync()
    {
        if (!IsVisionCapable) return;
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|WebP (*.webp)|*.webp|BMP (*.bmp)|*.bmp|所有文件|*.*",
                Multiselect = true,
                Title = "选择图片"
            };
            if (dlg.ShowDialog() != true) return;

            foreach (var path in dlg.FileNames)
            {
                try
                {
                    // Read + resize + base64 encode + thumbnail decode all on background thread
                    // so the UI thread stays responsive even for large images.
                    var img = await System.Threading.Tasks.Task.Run(() => LoadAndPrepareImage(path));
                    AttachedImages.Add(img);
                }
                catch (Exception ex)
                {
                    _log.Warn("Chat", $"Attach image failed for {path}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn("Chat", $"Attach image failed: {ex.Message}");
        }
    }

    private static AttachedImage LoadAndPrepareImage(string path)
    {
        var originalBytes = File.ReadAllBytes(path);
        var (resizedBytes, width, height) = ResizeImageIfNeeded(originalBytes, maxDimension: 1024);
        var b64 = Convert.ToBase64String(resizedBytes);
        var thumb = CreateThumbnailFromBytes(resizedBytes, maxPixel: 160);
        return new AttachedImage
        {
            Path = path,
            Base64 = b64,
            Thumbnail = thumb,
            OriginalSize = originalBytes.LongLength,
            ScaledWidth = width,
            ScaledHeight = height
        };
    }

    private static (byte[] bytes, int width, int height) ResizeImageIfNeeded(byte[] input, int maxDimension)
    {
        var decoder = BitmapDecoder.Create(
            new MemoryStream(input),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        int w = frame.PixelWidth;
        int h = frame.PixelHeight;
        if (w <= maxDimension && h <= maxDimension)
        {
            return (input, w, h);
        }
        double ratio = Math.Min((double)maxDimension / w, (double)maxDimension / h);
        int newW = Math.Max(1, (int)Math.Round(w * ratio));
        int newH = Math.Max(1, (int)Math.Round(h * ratio));
        var transform = new ScaleTransform(ratio, ratio);
        var resized = new TransformedBitmap(frame, transform);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(resized));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return (ms.ToArray(), newW, newH);
    }

    private static BitmapImage CreateThumbnailFromBytes(byte[] bytes, int maxPixel)
    {
        var bmp = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        bmp.BeginInit();
        bmp.DecodePixelWidth = maxPixel;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Draft) || string.IsNullOrWhiteSpace(SelectedModel)) return;

        var userText = Draft.Trim();
        var attachedImages = AttachedImages.ToList();
        Draft = string.Empty;
        AttachedImages.Clear();

        await SendCoreAsync(userText, attachedImages);
    }

    private async Task SendCoreAsync(string userText, List<AttachedImage> attachedImages)
    {
        if (string.IsNullOrWhiteSpace(SelectedModel)) return;

        var userTurn = new ChatTurnViewModel(ChatRole.User, userText);
        foreach (var img in attachedImages)
        {
            userTurn.Images.Add(img);
        }
        Turns.Add(userTurn);

        var assistantTurn = new ChatTurnViewModel(ChatRole.Assistant, string.Empty) { IsThinking = true };
        Turns.Add(assistantTurn);

        var messages = new List<ChatMessage>();
        foreach (var t in Turns.Where(t => t != assistantTurn))
        {
            var msg = new ChatMessage
            {
                Role = t.RoleEnum == ChatRole.Assistant ? "assistant" : "user",
                Content = t.Content
            };
            messages.Add(msg);
        }
        if (attachedImages.Count > 0 && EnableVision)
        {
            messages[^1].Images = attachedImages.Select(i => i.Base64).ToList();
        }

        var req = new ChatRequest
        {
            Model = SelectedModel,
            Messages = messages,
            Stream = true,
            KeepAlive = "5m",
            Think = IsThinkingCapable && EnableThinking ? true : null
        };

        _cts = new CancellationTokenSource();
        IsGenerating = true;
        IsThinking = true;
        StatusText = "正在思考...";
        FirstTokenMs = 0;
        TotalTps = 0;
        EvalCount = 0;
        assistantTurn.Stats = string.Empty;

        ScrollToEndIfNearBottomRequested?.Invoke();

        var buffer = new StringBuilder();
        var sw = Stopwatch.StartNew();
        bool firstTokenReceived = false;
        int evalCount = 0;
        long evalDurationNs = 0;
        long loadDurationNs = 0;
        try
        {
            await foreach (var chunk in _ollamaService.Client.ChatStreamAsync(req, _cts.Token).ConfigureAwait(true))
            {
                if (chunk.Message?.Content != null && chunk.Message.Content.Length > 0)
                {
                    if (!firstTokenReceived)
                    {
                        firstTokenReceived = true;
                        FirstTokenMs = sw.Elapsed.TotalMilliseconds;
                        IsThinking = false;
                        assistantTurn.IsThinking = false;
                        assistantTurn.IsStreaming = true;
                        StatusText = $"首字 {FirstTokenMs:0} ms · 生成中...";
                        assistantTurn.Stats = $"首字 {FirstTokenMs:0} ms";
                    }
                    buffer.Append(chunk.Message.Content);
                    assistantTurn.Content = buffer.ToString();
                    ScrollToEndIfNearBottomRequested?.Invoke();
                }
                if (chunk.Done)
                {
                    evalCount = chunk.EvalCount ?? 0;
                    if (chunk.EvalDuration.HasValue) evalDurationNs = chunk.EvalDuration.Value;
                    if (chunk.LoadDuration.HasValue) loadDurationNs = chunk.LoadDuration.Value;
                    EvalCount = evalCount;
                    if (evalDurationNs > 0 && evalCount > 0)
                    {
                        TotalTps = evalCount / (evalDurationNs / 1_000_000_000.0);
                    }
                    var loadMs = loadDurationNs / 1_000_000.0;
                    var stats = $"首字 {FirstTokenMs:0} ms · {TotalTps:0.0} t/s · 共 {evalCount} token";
                    if (loadMs > 0)
                    {
                        stats += $" · 加载 {loadMs:0} ms";
                    }
                    assistantTurn.Stats = stats;
                    StatusText = $"完成 · {stats}";
                }
            }
            if (!firstTokenReceived)
            {
                StatusText = "模型未产生响应";
                assistantTurn.Content = "（模型未产生响应）";
            }
        }
        catch (OperationCanceledException)
        {
            assistantTurn.Content = buffer.ToString() + "\n\n[已取消]";
            assistantTurn.Stats = "[已取消]";
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            _log.Error("Chat", $"Chat failed: {ex.Message}", ex);
            assistantTurn.Content = buffer.ToString() + $"\n\n[错误: {ex.Message}]";
            StatusText = $"错误: {ex.Message}";
        }
        finally
        {
            assistantTurn.IsThinking = false;
            assistantTurn.IsStreaming = false;
            IsGenerating = false;
            IsThinking = false;
            _cts?.Dispose();
            _cts = null;
            ScrollToEndRequested?.Invoke();
        }
    }

    private async Task CancelAsync()
    {
        _cts?.Cancel();
        await Task.Yield();
    }

    private async Task ClearAsync()
    {
        if (IsGenerating)
        {
            _cts?.Cancel();
            await Task.Delay(120);
        }
        Turns.Clear();
        AttachedImages.Clear();
        StatusText = string.Empty;
        FirstTokenMs = 0;
        TotalTps = 0;
        EvalCount = 0;
    }

    private void CopyTurn(object? parameter)
    {
        if (parameter is not ChatTurnViewModel turn) return;
        try
        {
            System.Windows.Clipboard.SetText(turn.Content ?? string.Empty);
            StatusText = "已复制到剪贴板";
        }
        catch (Exception ex)
        {
            _log.Warn("Chat", $"Copy failed: {ex.Message}");
            StatusText = "复制失败";
        }
    }

    private void DeleteTurn(object? parameter)
    {
        if (parameter is not ChatTurnViewModel turn) return;
        // If the user is deleting a User turn, also remove the trailing Assistant turn
        // (if any) so we don't leave an orphan response with no question.
        var idx = Turns.IndexOf(turn);
        if (idx < 0) return;
        Turns.RemoveAt(idx);
        if (turn.Role == ChatRole.User && idx < Turns.Count && Turns[idx].Role == ChatRole.Assistant)
        {
            Turns.RemoveAt(idx);
        }
        StatusText = "已删除";
    }

    private async Task RegenerateTurnAsync(object? parameter)
    {
        if (parameter is not ChatTurnViewModel assistantTurn) return;
        if (assistantTurn.Role != ChatRole.Assistant) return;
        if (IsGenerating) return;

        // Find the user turn immediately preceding this assistant turn.
        var idx = Turns.IndexOf(assistantTurn);
        if (idx <= 0) return;
        if (Turns[idx - 1] is not ChatTurnViewModel userTurn || userTurn.Role != ChatRole.User) return;

        var userText = userTurn.Content;
        var attachedImages = userTurn.Images.ToList();

        // Remove the user turn and this assistant turn — we will re-add them in SendCoreAsync.
        Turns.RemoveAt(idx);
        Turns.RemoveAt(idx - 1);

        await SendCoreAsync(userText, attachedImages);
    }
}
