namespace OllamaManager.Core.Models;

public enum ModelLifecycleStatus
{
    Unknown,
    NotLoaded,
    Loading,
    Running,
    Stopping,
    Unloading,
    Error
}
