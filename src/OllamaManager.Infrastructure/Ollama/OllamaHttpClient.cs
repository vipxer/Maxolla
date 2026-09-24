using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Exceptions;
using OllamaManager.Core.Interfaces;
using OllamaManager.Core.Models;

namespace OllamaManager.Infrastructure.Ollama;

public class OllamaHttpClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _longHttpClient;
    private readonly ILogger<OllamaHttpClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public string BaseUrl { get; private set; } = string.Empty;

    public OllamaHttpClient(HttpClient httpClient, ILogger<OllamaHttpClient> logger)
    {
        _httpClient = httpClient;
        // Loading a large model from disk can take several minutes (cold cache, big files,
        // fragmented SSD). The default 30s timeout always fires before Ollama finishes
        // loading, making every Run click show "load failed". Use a dedicated client with
        // a much longer timeout only for the load operation.
        _longHttpClient = new HttpClient
        {
            BaseAddress = httpClient.BaseAddress,
            Timeout = TimeSpan.FromMinutes(10)
        };
        _logger = logger;
    }

    public void SetBaseUrl(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _longHttpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/tags", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ping failed");
            return false;
        }
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/version", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GetVersion failed");
            return null;
        }
    }

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/tags", cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, "/api/tags", cancellationToken).ConfigureAwait(false);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var result = new List<ModelInfo>();
            if (doc.RootElement.TryGetProperty("models", out var models))
            {
                foreach (var m in models.EnumerateArray())
                {
                    var info = new ModelInfo
                    {
                        Name = m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Model = m.TryGetProperty("model", out var mm) ? mm.GetString() : null,
                        Size = m.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var s) ? s : 0,
                        Digest = m.TryGetProperty("digest", out var dg) ? dg.GetString() : null,
                    };
                    if (m.TryGetProperty("modified_at", out var mod))
                    {
                        if (mod.ValueKind == JsonValueKind.String && DateTime.TryParse(mod.GetString(), out var dt))
                        {
                            info.ModifiedAt = dt;
                        }
                    }
                    if (m.TryGetProperty("details", out var det))
                    {
                        info.Details = ParseDetails(det);
                    }
                    result.Add(info);
                }
            }
            return result;
        }
        catch (OllamaApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetModels failed");
            throw new OllamaApiException("Failed to get models from Ollama", ex, endpoint: "/api/tags");
        }
    }

    public async Task<List<RunningModelInfo>> GetRunningModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/ps", cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, "/api/ps", cancellationToken).ConfigureAwait(false);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var result = new List<RunningModelInfo>();
            if (doc.RootElement.TryGetProperty("models", out var models))
            {
                foreach (var m in models.EnumerateArray())
                {
                    var info = new RunningModelInfo
                    {
                        Name = m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Model = m.TryGetProperty("model", out var mm) ? mm.GetString() : null,
                        Size = m.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var s) ? s : 0,
                        SizeVram = m.TryGetProperty("size_vram", out var sv) && sv.TryGetInt64(out var s2) ? s2 : 0,
                        Digest = m.TryGetProperty("digest", out var dg) ? dg.GetString() : null,
                    };
                    if (m.TryGetProperty("expires_at", out var exp) && exp.ValueKind == JsonValueKind.String && DateTime.TryParse(exp.GetString(), out var dt))
                    {
                        info.ExpiresAt = dt;
                    }
                    if (m.TryGetProperty("context", out var ctx) && ctx.ValueKind == JsonValueKind.Array)
                    {
                        info.Context = ctx.EnumerateArray().Take(1).Select(x => x.GetInt32()).ToList();
                    }
                    result.Add(info);
                }
            }
            return result;
        }
        catch (OllamaApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetRunningModels failed");
            throw new OllamaApiException("Failed to get running models from Ollama", ex, endpoint: "/api/ps");
        }
    }

    public async Task<ModelDetails?> GetModelDetailsAsync(string modelName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return null;
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/show", new { name = modelName, verbose = true }, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            await EnsureSuccessAsync(response, "/api/show", cancellationToken).ConfigureAwait(false);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ParseDetails(doc.RootElement);
        }
        catch (OllamaApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetModelDetails failed for {Model}", modelName);
            throw new OllamaApiException($"Failed to get model details for {modelName}", ex, endpoint: "/api/show");
        }
    }

    public async Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/generate", request, JsonOptions, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, "/api/generate", cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return result ?? new GenerateResponse();
        }
        catch (OllamaApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generate failed");
            throw new OllamaApiException("Generate request failed", ex, endpoint: "/api/generate");
        }
    }

    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Serialize the request on a background thread — JsonContent.Create blocks the
        // calling thread while it copies the entire body to a MemoryStream. For requests
        // carrying base64 images (several MB) this can block the UI thread for multiple
        // seconds and Windows flags the app "not responding". Offloading to the thread
        // pool keeps the UI responsive.
        var content = await System.Threading.Tasks.Task.Run(() =>
            JsonContent.Create(request, options: JsonOptions), cancellationToken).ConfigureAwait(false);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = content
        };
        httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/x-ndjson"));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = string.Empty;
            try { body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); } catch { }
            throw new OllamaApiException(
                $"Ollama chat API call failed with status {(int)response.StatusCode} {response.ReasonPhrase}",
                (int)response.StatusCode,
                "/api/chat",
                body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        var buffer = new System.Text.StringBuilder();
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            ChatStreamChunk? chunk = null;
            try
            {
                chunk = System.Text.Json.JsonSerializer.Deserialize<ChatStreamChunk>(line, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse chat chunk: {Line}", line);
                continue;
            }
            if (chunk != null)
            {
                yield return chunk;
            }
        }
    }

    public async Task<bool> CreateModelAsync(string name, string modelfileContent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/create", new { name = name, modelfile = modelfileContent, stream = false }, JsonOptions, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateModel failed");
            return false;
        }
    }

    public async Task<bool> DeleteModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, "/api/delete")
            {
                Content = JsonContent.Create(new { name = modelName }, options: JsonOptions)
            };
            using var response = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteModel failed");
            return false;
        }
    }

    public async Task<bool> CopyModelAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/copy", new { source = source, destination = destination }, JsonOptions, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CopyModel failed");
            return false;
        }
    }

    public async Task<bool> PullModelAsync(string modelName, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/pull")
            {
                Content = JsonContent.Create(new { name = modelName, stream = false }, options: JsonOptions)
            };
            using var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PullModel failed");
            return false;
        }
    }

    public async Task<bool> UnloadModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/generate", new { model = modelName, prompt = "", keep_alive = "0", stream = false }, JsonOptions, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UnloadModel failed for {Model}", modelName);
            return false;
        }
    }

    public async Task<bool> LoadModelAsync(string modelName, TimeSpan? keepAlive = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var keepAliveValue = keepAlive.HasValue ? $"{keepAlive.Value.TotalSeconds}s" : "5m";
            using var response = await _longHttpClient.PostAsJsonAsync("/api/generate", new { model = modelName, prompt = "", keep_alive = keepAliveValue, stream = false }, JsonOptions, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadModel failed for {Model}", modelName);
            return false;
        }
    }

    private static ModelDetails ParseDetails(JsonElement det)
    {
        var details = new ModelDetails();
        if (det.TryGetProperty("format", out var f)) details.Format = f.GetString();
        if (det.TryGetProperty("family", out var fm)) details.Family = fm.GetString();
        if (det.TryGetProperty("families", out var fms) && fms.ValueKind == JsonValueKind.Array)
        {
            details.Families = fms.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        }
        if (det.TryGetProperty("parameter_size", out var ps)) details.ParameterSize = ps.GetString();
        if (det.TryGetProperty("quantization_level", out var ql)) details.QuantizationLevel = ql.GetString();
        if (det.TryGetProperty("parent_model", out var pm)) details.ParentModel = pm.GetString();
        if (det.TryGetProperty("template", out var tpl)) details.Template = tpl.GetString();
        if (det.TryGetProperty("system", out var sys)) details.System = sys.GetString();
        if (det.TryGetProperty("license", out var lic)) details.License = lic.GetString();
        if (det.TryGetProperty("modelfile", out var mf)) details.Modelfile = mf.GetString();
        if (det.TryGetProperty("parameters", out var prm)) details.Parameters = prm.GetString();
        if (det.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var s)) details.Size = s;
        if (det.TryGetProperty("digest", out var dg)) details.Digest = dg.GetString();
        if (det.TryGetProperty("capabilities", out var caps) && caps.ValueKind == JsonValueKind.Array)
        {
            details.Capabilities = caps.EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        return details;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string endpoint, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = string.Empty;
        try { body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); } catch { }
        throw new OllamaApiException(
            $"Ollama API call failed with status {(int)response.StatusCode} {response.ReasonPhrase}",
            (int)response.StatusCode,
            endpoint,
            body);
    }
}
