using System.Text.Json.Serialization;

namespace OllamaManager.Core.Models;

public class ModelDetails
{
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("families")]
    public List<string>? Families { get; set; }

    [JsonPropertyName("parameter_size")]
    public string? ParameterSize { get; set; }

    [JsonPropertyName("quantization_level")]
    public string? QuantizationLevel { get; set; }

    [JsonPropertyName("parent_model")]
    public string? ParentModel { get; set; }

    [JsonPropertyName("template")]
    public string? Template { get; set; }

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("license")]
    public string? License { get; set; }

    [JsonPropertyName("modelfile")]
    public string? Modelfile { get; set; }

    [JsonPropertyName("parameters")]
    public string? Parameters { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("digest")]
    public string? Digest { get; set; }

    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }
}
