using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;
using LogLevel = OllamaManager.Core.Interfaces.LogLevel;

namespace OllamaManager.Infrastructure.Logging;

public class FileLogService : ILogService, ILoggerProvider, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly string _logsDir;
    private readonly ConcurrentQueue<LogEntry> _inMemoryBuffer = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _writeLock = new();
    private StreamWriter? _currentWriter;
    private string _currentLogFile = string.Empty;
    private DateTime _currentLogDate = DateTime.MinValue;
    private long _currentLogSize = 0;
    private const int BufferMax = 2000;

    public FileLogService()
    {
        _settingsService = null!;
        _logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OllamaManager", "Logs");
        try { Directory.CreateDirectory(_logsDir); } catch { }
    }

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    public void Dispose()
    {
        lock (_writeLock)
        {
            foreach (var l in _loggers.Values)
            {
                try { l.Dispose(); } catch { }
            }
            _loggers.Clear();
            _currentWriter?.Dispose();
            _currentWriter = null;
        }
    }

    public void Info(string category, string message) => Write(LogLevel.Information, category, message, null);
    public void Warn(string category, string message, Exception? ex = null) => Write(LogLevel.Warning, category, message, ex);
    public void Error(string category, string message, Exception? ex = null) => Write(LogLevel.Error, category, message, ex);
    public void Debug(string category, string message) => Write(LogLevel.Debug, category, message, null);
    public void Trace(string category, string message) => Write(LogLevel.Trace, category, message, null);

    public string GetLogsDirectory() => _logsDir;

    public void OpenLogsDirectory()
    {
        try
        {
            if (!Directory.Exists(_logsDir)) Directory.CreateDirectory(_logsDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _logsDir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public void ClearAllLogs()
    {
        try
        {
            while (_inMemoryBuffer.TryDequeue(out _)) { }

            var dir = new DirectoryInfo(_logsDir);
            if (!dir.Exists) return;

            lock (_writeLock)
            {
                try { _currentWriter?.Dispose(); } catch { }
                _currentWriter = null;
                _currentLogSize = 0;

                foreach (var f in dir.GetFiles("*.log"))
                {
                    try { f.Delete(); } catch { }
                }
            }
        }
        catch { }
    }

    public List<LogEntry> GetRecentEntries(int maxEntries = 500)
    {
        var list = _inMemoryBuffer.ToArray().ToList();
        if (list.Count > maxEntries) list = list.GetRange(list.Count - maxEntries, maxEntries);
        return list.OrderByDescending(e => e.Timestamp).Take(maxEntries).ToList();
    }

    public List<LogEntry> SearchEntries(string query, int maxEntries = 500)
    {
        if (string.IsNullOrWhiteSpace(query)) return GetRecentEntries(maxEntries);
        var recent = GetRecentEntries(maxEntries);
        return recent.Where(e => e.Message.Contains(query, StringComparison.OrdinalIgnoreCase) || e.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Category = category,
            Message = message,
            Exception = exception?.ToString()
        };

        _inMemoryBuffer.Enqueue(entry);
        while (_inMemoryBuffer.Count > BufferMax && _inMemoryBuffer.TryDequeue(out _)) { }

        try
        {
            WriteToFile(entry);
        }
        catch { }
    }

    private void WriteToFile(LogEntry entry)
    {
        lock (_writeLock)
        {
            EnsureWriter();
            if (_currentWriter == null) return;
            var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level,-11}] [{entry.Category}] {entry.Message}";
            _currentWriter.WriteLine(line);
            if (!string.IsNullOrEmpty(entry.Exception))
            {
                foreach (var l in entry.Exception.Split('\n'))
                {
                    _currentWriter.WriteLine($"    {l.TrimEnd()}");
                }
            }
            _currentWriter.Flush();
            _currentLogSize += Encoding.UTF8.GetByteCount(line) + 1;
            long maxBytes = 10L * 1024L * 1024L;
            try { maxBytes = (_settingsService?.Current.MaxLogFileSizeMB ?? 10) * 1024L * 1024L; } catch { }
            if (_currentLogSize >= maxBytes)
            {
                _currentWriter.Dispose();
                _currentWriter = null;
                ClearAllLogs();
            }
        }
    }

    private void EnsureWriter()
    {
        var today = DateTime.Today;
        var expectedFile = Path.Combine(_logsDir, $"ollama-manager-{today:yyyy-MM-dd}.log");
        if (_currentWriter != null && _currentLogDate == today && _currentLogFile == expectedFile) return;

        try { _currentWriter?.Dispose(); } catch { }
        _currentLogDate = today;
        _currentLogFile = expectedFile;
        _currentLogSize = 0;
        try
        {
            if (File.Exists(expectedFile))
            {
                _currentLogSize = new FileInfo(expectedFile).Length;
            }
            var stream = new FileStream(expectedFile, FileMode.Append, FileAccess.Write, FileShare.Read);
            _currentWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
        }
        catch
        {
            _currentWriter = null;
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLogService _parent;
        public FileLogger(string category, FileLogService parent) { _category = category; _parent = parent; }
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel != Microsoft.Extensions.Logging.LogLevel.None;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var mapped = logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Debug => LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => LogLevel.Information,
                Microsoft.Extensions.Logging.LogLevel.Warning => LogLevel.Warning,
                Microsoft.Extensions.Logging.LogLevel.Error => LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => LogLevel.Critical,
                _ => LogLevel.Information
            };
            _parent.Write(mapped, _category, formatter(state, exception), exception);
        }

        public void Dispose() { }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}