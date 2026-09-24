namespace OllamaManager.Core.Models;

public enum DiagnosticSeverity
{
    Info,
    Ok,
    Warning,
    Error
}

public class DiagnosticItem
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DiagnosticSeverity Severity { get; set; }
    public string? Detail { get; set; }
    public string? Recommendation { get; set; }

    public string SeveritySymbol => Severity switch
    {
        DiagnosticSeverity.Ok => "[OK]",
        DiagnosticSeverity.Warning => "[WARN]",
        DiagnosticSeverity.Error => "[ERR]",
        _ => "[INFO]"
    };
}

public class DiagnosticReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public List<DiagnosticItem> Items { get; set; } = new();

    public bool HasErrors => Items.Any(i => i.Severity == DiagnosticSeverity.Error);
    public bool HasWarnings => Items.Any(i => i.Severity == DiagnosticSeverity.Warning);
    public int ErrorCount => Items.Count(i => i.Severity == DiagnosticSeverity.Error);
    public int WarningCount => Items.Count(i => i.Severity == DiagnosticSeverity.Warning);
    public int OkCount => Items.Count(i => i.Severity == DiagnosticSeverity.Ok);

    public string ToPlainText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Ollama Manager Diagnostic Report");
        sb.AppendLine($"Generated: {GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', 50));
        foreach (var item in Items)
        {
            sb.AppendLine($"{item.SeveritySymbol} [{item.Category}] {item.Title}");
            if (!string.IsNullOrWhiteSpace(item.Detail))
            {
                sb.AppendLine($"    {item.Detail}");
            }
            if (!string.IsNullOrWhiteSpace(item.Recommendation))
            {
                sb.AppendLine($"    Recommend: {item.Recommendation}");
            }
        }
        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"Summary: {OkCount} OK, {WarningCount} Warnings, {ErrorCount} Errors");
        return sb.ToString();
    }
}
