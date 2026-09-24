using OllamaManager.Core.Models;

namespace OllamaManager.Tests;

public class DiagnosticReportTests
{
    [Fact]
    public void EmptyReport_NoErrorsOrWarnings()
    {
        var r = new DiagnosticReport();
        Assert.False(r.HasErrors);
        Assert.False(r.HasWarnings);
        Assert.Equal(0, r.ErrorCount);
    }

    [Fact]
    public void HasErrors_TrueWhenAnyErrorItem()
    {
        var r = new DiagnosticReport
        {
            Items = new List<DiagnosticItem>
            {
                new() { Severity = DiagnosticSeverity.Ok, Title = "A" },
                new() { Severity = DiagnosticSeverity.Error, Title = "B" }
            }
        };
        Assert.True(r.HasErrors);
        Assert.Equal(1, r.ErrorCount);
        Assert.Equal(1, r.OkCount);
    }

    [Fact]
    public void SeveritySymbol_FormatsCorrectly()
    {
        Assert.Equal("[OK]", new DiagnosticItem { Severity = DiagnosticSeverity.Ok }.SeveritySymbol);
        Assert.Equal("[WARN]", new DiagnosticItem { Severity = DiagnosticSeverity.Warning }.SeveritySymbol);
        Assert.Equal("[ERR]", new DiagnosticItem { Severity = DiagnosticSeverity.Error }.SeveritySymbol);
        Assert.Equal("[INFO]", new DiagnosticItem { Severity = DiagnosticSeverity.Info }.SeveritySymbol);
    }
}