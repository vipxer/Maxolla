namespace OllamaManager.Core.Interfaces;

public interface IProcessService
{
    Task<ProcessResult> RunAsync(string executable, IEnumerable<string>? args = null, string? workingDirectory = null, TimeSpan? timeout = null, IDictionary<string, string>? environmentVariables = null, CancellationToken cancellationToken = default);
    bool IsRunning(string processName);
    int? GetProcessId(string processName);
    string? GetExecutablePath(string processName);
    IEnumerable<RunningProcessInfo> FindProcesses(string processName);
    bool StopProcessTree(int pid, bool force = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}

public class ProcessResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool TimedOut { get; set; }
    public TimeSpan Elapsed { get; set; }
    public bool Success => ExitCode == 0 && !TimedOut;
}

public class RunningProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public int? ParentPid { get; set; }
}
