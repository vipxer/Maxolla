using System.Text.Json.Serialization;

namespace OllamaManager.Core.Models;

public class ModelfileParameter
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ParameterType Type { get; set; } = ParameterType.String;
    public string? Description { get; set; }
}

public enum ParameterType
{
    Integer,
    Float,
    Boolean,
    String,
    Enum
}

public class Modelfile
{
    public string From { get; set; } = string.Empty;
    public string? Template { get; set; }
    public string? System { get; set; }
    public string? License { get; set; }
    public string? Adapter { get; set; }
    public List<ModelfileMessage> Messages { get; set; } = new();
    public List<ModelfileParameter> Parameters { get; set; } = new();
    public List<string> RawLines { get; set; } = new();
    public List<string> UnknownLines { get; set; } = new();

    public string ToModelfileText()
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(From))
        {
            sb.AppendLine($"FROM {From}");
        }
        if (!string.IsNullOrWhiteSpace(Template))
        {
            sb.AppendLine($"TEMPLATE \"\"\"{Template}\"\"\"");
        }
        if (!string.IsNullOrWhiteSpace(System))
        {
            sb.AppendLine($"SYSTEM \"\"\"{System}\"\"\"");
        }
        if (!string.IsNullOrWhiteSpace(License))
        {
            sb.AppendLine($"LICENSE \"\"\"{License}\"\"\"");
        }
        if (!string.IsNullOrWhiteSpace(Adapter))
        {
            sb.AppendLine($"ADAPTER {Adapter}");
        }
        foreach (var msg in Messages)
        {
            sb.AppendLine($"MESSAGE {msg.Role} \"\"\"{msg.Content}\"\"\"");
        }
        foreach (var p in Parameters)
        {
            sb.AppendLine($"PARAMETER {p.Key} {p.Value}");
        }
        foreach (var line in UnknownLines)
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }
}

public class ModelfileMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ModelfileValidation
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class GenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("keep_alive")]
    public string? KeepAlive { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, object>? Options { get; set; }

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("template")]
    public string? Template { get; set; }

    [JsonPropertyName("context")]
    public List<int>? Context { get; set; }
}

public class GenerateResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }

    [JsonPropertyName("context")]
    public List<int>? Context { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    [JsonPropertyName("prompt_eval_duration")]
    public long? PromptEvalDuration { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }

    [JsonPropertyName("eval_duration")]
    public long? EvalDuration { get; set; }
}

public enum ChatRole
{
    System,
    User,
    Assistant
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    public ChatRole RoleEnum
    {
        get => Role switch
        {
            "system" => ChatRole.System,
            "assistant" => ChatRole.Assistant,
            _ => ChatRole.User
        };
        set => Role = value switch
        {
            ChatRole.System => "system",
            ChatRole.Assistant => "assistant",
            _ => "user"
        };
    }
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    [JsonPropertyName("keep_alive")]
    public string? KeepAlive { get; set; }

    [JsonPropertyName("think")]
    public bool? Think { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, object>? Options { get; set; }
}

public class ChatStreamChunk
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    [JsonPropertyName("prompt_eval_duration")]
    public long? PromptEvalDuration { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }

    [JsonPropertyName("eval_duration")]
    public long? EvalDuration { get; set; }
}
