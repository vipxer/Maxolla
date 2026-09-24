using OllamaManager.Core.Models;

namespace OllamaManager.Core.Interfaces;

public interface IDiagnosticsService
{
    Task<DiagnosticReport> RunFullDiagnosticAsync(CancellationToken cancellationToken = default);
    string FormatReportAsText(DiagnosticReport report, bool anonymize = true);
    string FormatReportAsJson(DiagnosticReport report, bool anonymize = true);
}
