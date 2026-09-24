namespace OllamaManager.Core.Models;

public class GpuInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DriverVersion { get; set; }
    public double Temperature { get; set; }
    public double GpuUtilization { get; set; }
    public long MemoryTotal { get; set; }
    public long MemoryUsed { get; set; }
    public long MemoryFree => Math.Max(0, MemoryTotal - MemoryUsed);
    public double PowerUsage { get; set; }
    public double PowerLimit { get; set; }
    public long GraphicsClock { get; set; }
    public long MemoryClock { get; set; }
    public string Vendor { get; set; } = "Unknown";
    public List<GpuProcessInfo> Processes { get; set; } = new();

    public string DisplayMemoryTotal => ModelInfo.FormatBytes(MemoryTotal);
    public string DisplayMemoryUsed => ModelInfo.FormatBytes(MemoryUsed);
    public string DisplayMemoryFree => ModelInfo.FormatBytes(MemoryFree);
    public string DisplayMemoryUsage => $"{DisplayMemoryUsed} / {DisplayMemoryTotal}";
    public double MemoryUtilizationPercent => MemoryTotal > 0 ? (MemoryUsed * 100.0 / MemoryTotal) : 0;
}

public class GpuProcessInfo
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public long MemoryUsed { get; set; }
    public bool IsOllamaRelated { get; set; }
    public string DisplayMemory => ModelInfo.FormatBytes(MemoryUsed);
}
