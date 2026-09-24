using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Exceptions;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;
using ModelfileModel = OllamaManager.Core.Models.Modelfile;
using ModelfileParameterModel = OllamaManager.Core.Models.ModelfileParameter;

namespace OllamaManager.Infrastructure.Modelfile;

public class ModelfileService : IModelfileService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly ILogger<ModelfileService> _logger;
    private static readonly HashSet<string> KnownInstructions = new(StringComparer.OrdinalIgnoreCase)
    {
        "FROM", "TEMPLATE", "SYSTEM", "LICENSE", "ADAPTER", "MESSAGE", "PARAMETER", "STOP"
    };

    public ModelfileService(IOllamaClient ollamaClient, ILogger<ModelfileService> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<ModelfileModel> ParseAsync(string text, CancellationToken cancellationToken = default)
    {
        var modelfile = new ModelfileModel();
        if (string.IsNullOrWhiteSpace(text)) return modelfile;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.Trim();
            modelfile.RawLines.Add(rawLine);

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            try
            {
                var parts = SplitTopLevel(line);
                if (parts.Count == 0) continue;
                var instruction = parts[0].ToUpperInvariant();
                var rest = string.Join(" ", parts.Skip(1));

                switch (instruction)
                {
                    case "FROM":
                        modelfile.From = rest.Trim().Trim('"');
                        break;
                    case "TEMPLATE":
                        modelfile.Template = rest.Trim().Trim('"');
                        break;
                    case "SYSTEM":
                        modelfile.System = rest.Trim().Trim('"');
                        break;
                    case "LICENSE":
                        modelfile.License = rest.Trim().Trim('"');
                        break;
                    case "ADAPTER":
                        modelfile.Adapter = rest.Trim().Trim('"');
                        break;
                    case "MESSAGE":
                        {
                            var msgParts = rest.Split(' ', 2);
                            if (msgParts.Length == 2)
                            {
                                modelfile.Messages.Add(new ModelfileMessage
                                {
                                    Role = msgParts[0],
                                    Content = msgParts[1].Trim().Trim('"')
                                });
                            }
                            break;
                        }
                    case "PARAMETER":
                        {
                            var pParts = rest.Split(' ', 2);
                            if (pParts.Length == 2)
                            {
                                modelfile.Parameters.Add(new ModelfileParameter
                                {
                                    Key = pParts[0],
                                    Value = pParts[1].Trim().Trim('"')
                                });
                            }
                            break;
                        }
                    case "STOP":
                        modelfile.Parameters.Add(new ModelfileParameter
                        {
                            Key = "stop",
                            Value = rest.Trim().Trim('"')
                        });
                        break;
                    default:
                        modelfile.UnknownLines.Add(line);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse line {Line}", i + 1);
                modelfile.UnknownLines.Add(line);
            }
        }

        await Task.CompletedTask;
        return modelfile;
    }

    public string Serialize(ModelfileModel modelfile)
    {
        return modelfile.ToModelfileText();
    }

    public async Task<ModelfileValidation> ValidateAsync(string text, CancellationToken cancellationToken = default)
    {
        var validation = new ModelfileValidation { IsValid = true };
        var modelfile = await ParseAsync(text, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(modelfile.From))
        {
            validation.IsValid = false;
            validation.Errors.Add("Modelfile must have a FROM instruction");
        }
        foreach (var u in modelfile.UnknownLines)
        {
            validation.Warnings.Add($"Unknown line preserved: {u}");
        }
        return validation;
    }

    public Task<List<ModelfileParameter>> GetAvailableParametersAsync()
    {
        var list = new List<ModelfileParameter>
        {
            new() { Key = "temperature", Type = ParameterType.Float, Description = "Sampling temperature (0 = deterministic)" },
            new() { Key = "top_p", Type = ParameterType.Float, Description = "Nucleus sampling threshold" },
            new() { Key = "top_k", Type = ParameterType.Integer, Description = "Top-K sampling" },
            new() { Key = "min_p", Type = ParameterType.Float, Description = "Min-P sampling" },
            new() { Key = "repeat_penalty", Type = ParameterType.Float, Description = "Repetition penalty" },
            new() { Key = "repeat_last_n", Type = ParameterType.Integer, Description = "Tokens to consider for repetition" },
            new() { Key = "seed", Type = ParameterType.Integer, Description = "Random seed (-1 for random)" },
            new() { Key = "num_ctx", Type = ParameterType.Integer, Description = "Context window size" },
            new() { Key = "num_predict", Type = ParameterType.Integer, Description = "Max tokens to predict (-1 = unlimited)" },
            new() { Key = "num_gpu", Type = ParameterType.Integer, Description = "Number of GPU layers" },
            new() { Key = "num_thread", Type = ParameterType.Integer, Description = "CPU threads" },
            new() { Key = "stop", Type = ParameterType.String, Description = "Stop sequences" },
            new() { Key = "mirostat", Type = ParameterType.Integer, Description = "Mirostat mode (0/1/2)" },
            new() { Key = "mirostat_eta", Type = ParameterType.Float, Description = "Mirostat eta" },
            new() { Key = "mirostat_tau", Type = ParameterType.Float, Description = "Mirostat tau" },
            new() { Key = "tfs_z", Type = ParameterType.Float, Description = "Tail free sampling" },
            new() { Key = "typical_p", Type = ParameterType.Float, Description = "Typical P sampling" }
        };
        return Task.FromResult(list);
    }

    public async Task<string> GenerateFromParametersAsync(string from, Dictionary<string, object> parameters, string? system = null, string? template = null, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"FROM {from}");
        if (!string.IsNullOrWhiteSpace(template))
        {
            sb.AppendLine($"TEMPLATE \"\"\"{template}\"\"\"");
        }
        if (!string.IsNullOrWhiteSpace(system))
        {
            sb.AppendLine($"SYSTEM \"\"\"{system}\"\"\"");
        }
        foreach (var kv in parameters)
        {
            var value = kv.Value switch
            {
                bool b => b ? "true" : "false",
                null => string.Empty,
                _ => kv.Value.ToString() ?? string.Empty
            };
            sb.AppendLine($"PARAMETER {kv.Key} {value}");
        }
        await Task.CompletedTask;
        return sb.ToString();
    }

    public async Task<bool> CreateModelAsync(string name, string modelfileText, CancellationToken cancellationToken = default)
    {
        return await _ollamaClient.CreateModelAsync(name, modelfileText, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ReadModelfileFromDiskAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteModelfileToDiskAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    private static List<string> SplitTopLevel(string line)
    {
        var parts = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0) parts.Add(current.ToString());
        return parts;
    }
}
