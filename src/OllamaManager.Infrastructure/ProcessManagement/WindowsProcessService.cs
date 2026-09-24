using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;

namespace OllamaManager.Infrastructure.ProcessManagement;

public class WindowsProcessService : IProcessService
{
    private readonly ILogger<WindowsProcessService> _logger;

    public WindowsProcessService(ILogger<WindowsProcessService> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessResult> RunAsync(string executable, IEnumerable<string>? args = null, string? workingDirectory = null, TimeSpan? timeout = null, IDictionary<string, string>? environmentVariables = null, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }
        if (args != null)
        {
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }
        if (environmentVariables != null)
        {
            foreach (var kv in environmentVariables)
            {
                startInfo.Environment[kv.Key] = kv.Value;
            }
        }

        var sw = Stopwatch.StartNew();
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {executable}");
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch {Executable}", executable);
            return new ProcessResult { ExitCode = -1, StandardOutput = string.Empty, StandardError = ex.Message, Elapsed = sw.Elapsed };
        }

        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(2);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception killEx)
            {
                _logger.LogWarning(killEx, "Failed to kill timed-out process");
            }
            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
                TimedOut = true,
                Elapsed = sw.Elapsed
            };
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            Elapsed = sw.Elapsed
        };
    }

    public bool IsRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check process: {Name}", processName);
            return false;
        }
    }

    public int? GetProcessId(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.Length > 0 ? processes[0].Id : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get pid for: {Name}", processName);
            return null;
        }
    }

    public string? GetExecutablePath(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0) return null;
            return processes[0].MainModule?.FileName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get path for: {Name}", processName);
            return null;
        }
    }

    public IEnumerable<RunningProcessInfo> FindProcesses(string processName)
    {
        var result = new List<RunningProcessInfo>();
        try
        {
            foreach (var p in Process.GetProcessesByName(processName))
            {
                string? path = null;
                int? parentPid = null;
                try { path = p.MainModule?.FileName; } catch { }
                try { parentPid = GetParentProcessId(p.Id); } catch { }
                result.Add(new RunningProcessInfo
                {
                    Pid = p.Id,
                    Name = p.ProcessName,
                    ExecutablePath = path,
                    ParentPid = parentPid
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate: {Name}", processName);
        }
        return result;
    }

    public bool StopProcessTree(int pid, bool force = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            if (process.HasExited) return true;

            if (!force)
            {
                try
                {
                    process.CloseMainWindow();
                    var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
                    if (process.WaitForExit((int)effectiveTimeout.TotalMilliseconds))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Graceful close failed for pid {Pid}", pid);
                }
            }

            try
            {
                process.Kill(entireProcessTree: true);
                return process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Force kill failed for pid {Pid}", pid);
                return false;
            }
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop pid {Pid}", pid);
            return false;
        }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref int processInformation, int processInformationLength, out int returnLength);

    private static int? GetParentProcessId(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            var handle = process.Handle;
            var parentPid = 0;
            var size = Marshal.SizeOf(parentPid);
            var result = NtQueryInformationProcess(handle, 0, ref parentPid, size, out _);
            return result == 0 ? parentPid : null;
        }
        catch
        {
            return null;
        }
    }
}
