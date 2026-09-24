using System.Text.Json.Serialization;

namespace OllamaManager.Core.Models;

public class ModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("digest")]
    public string? Digest { get; set; }

    [JsonPropertyName("modified_at")]
    public DateTime? ModifiedAt { get; set; }

    [JsonPropertyName("details")]
    public ModelDetails? Details { get; set; }

    [JsonIgnore]
    public ModelLifecycleStatus Status { get; set; } = ModelLifecycleStatus.Unknown;

    [JsonIgnore]
    public bool IsRunning => Status == ModelLifecycleStatus.Running;

    [JsonIgnore]
    public string DisplaySize => FormatBytes(Size);

    [JsonIgnore]
    public string Category { get; set; } = "general";

    [JsonIgnore]
    public string CategoryIcon => Category switch
    {
        "code" => "💻",
        "vision" => "👁",
        "embedding" => "📊",
        "math" => "🔢",
        "chat" => "💬",
        _ => "📦"
    };

    [JsonIgnore]
    public int ItemNumber { get; set; }

    [JsonIgnore]
    public string ItemNumberDisplay => ItemNumber > 0 ? $"[{ItemNumber}]" : string.Empty;

    public static string ClassifyCategory(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return "general";
        var n = modelName.ToLowerInvariant();
        if (n.Contains("embed") || n.Contains("bge") || n.Contains("nomic-embed") || n.Contains("e5-")) return "embedding";
        if (n.Contains("vision") || n.Contains("-vl") || n.Contains("llava") || n.Contains("minicpm-v") || n.EndsWith(":vision")) return "vision";
        if (n.Contains("code") || n.Contains("coder") || n.Contains("codellama") || n.Contains("starcoder") || n.Contains("deepseek-coder")) return "code";
        if (n.Contains("math")) return "math";
        return "general";
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
