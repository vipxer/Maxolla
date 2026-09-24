namespace OllamaManager.Core.Models;

public enum OllamaStatusKind
{
    NotInstalled,
    InstalledButNotRunning,
    RunningButApiUnavailable,
    ApiAvailable,
    Unknown
}

public class OllamaStatus
{
    public OllamaStatusKind Kind { get; set; } = OllamaStatusKind.Unknown;
    public string? ExecutablePath { get; set; }
    public string? Version { get; set; }
    public string? ApiEndpoint { get; set; }
    public string? ModelsDirectory { get; set; }
    public string? DiscoveryMethod { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.Now;
    public string? ErrorMessage { get; set; }

    public bool IsApiAvailable => Kind == OllamaStatusKind.ApiAvailable;
    public bool IsRunning => Kind == OllamaStatusKind.ApiAvailable || Kind == OllamaStatusKind.RunningButApiUnavailable;

    public string DisplayStatusText => Kind switch
    {
        OllamaStatusKind.NotInstalled => "NotInstalled",
        OllamaStatusKind.InstalledButNotRunning => "InstalledNotRunning",
        OllamaStatusKind.RunningButApiUnavailable => "RunningApiUnavailable",
        OllamaStatusKind.ApiAvailable => "Running",
        _ => "Unknown"
    };
}
