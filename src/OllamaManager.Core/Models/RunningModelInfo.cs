using System.Text.Json.Serialization;

namespace OllamaManager.Core.Models;

public class RunningModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("size_vram")]
    public long SizeVram { get; set; }

    [JsonPropertyName("digest")]
    public string? Digest { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("context")]
    public List<int>? Context { get; set; }

    [JsonIgnore]
    public int? ContextLength => Context is { Count: > 0 } ? Context[0] : null;

    [JsonIgnore]
    public string DisplaySize => ModelInfo.FormatBytes(Size);

    [JsonIgnore]
    public string DisplayVram => ModelInfo.FormatBytes(SizeVram);
}
