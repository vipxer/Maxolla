namespace OllamaManager.Core.Interfaces;

public interface ILogService
{
    void Info(string category, string message);
    void Warn(string category, string message, Exception? ex = null);
    void Error(string category, string message, Exception? ex = null);
    void Debug(string category, string message);
    void Trace(string category, string message);

    string GetLogsDirectory();
    void OpenLogsDirectory();
    void ClearAllLogs();

    List<LogEntry> GetRecentEntries(int maxEntries = 500);
    List<LogEntry> SearchEntries(string query, int maxEntries = 500);
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
