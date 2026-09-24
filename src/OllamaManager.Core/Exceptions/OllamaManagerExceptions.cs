namespace OllamaManager.Core.Exceptions;

public class OllamaManagerException : Exception
{
    public OllamaManagerException(string message) : base(message) { }
    public OllamaManagerException(string message, Exception inner) : base(message, inner) { }
}

public class OllamaNotFoundException : OllamaManagerException
{
    public OllamaNotFoundException() : base("Ollama executable was not found.") { }
    public OllamaNotFoundException(string message) : base(message) { }
}

public class OllamaApiException : OllamaManagerException
{
    public int? StatusCode { get; }
    public string? Endpoint { get; }
    public string? ResponseBody { get; }

    public OllamaApiException(string message, int? statusCode = null, string? endpoint = null, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        Endpoint = endpoint;
        ResponseBody = responseBody;
    }

    public OllamaApiException(string message, Exception inner, int? statusCode = null, string? endpoint = null, string? responseBody = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Endpoint = endpoint;
        ResponseBody = responseBody;
    }
}

public class OllamaTimeoutException : OllamaManagerException
{
    public OllamaTimeoutException(string message) : base(message) { }
    public OllamaTimeoutException(string message, Exception inner) : base(message, inner) { }
}

public class ModelNotFoundException : OllamaManagerException
{
    public string ModelName { get; }
    public ModelNotFoundException(string modelName) : base($"Model not found: {modelName}")
    {
        ModelName = modelName;
    }
}

public class ModelfileParseException : OllamaManagerException
{
    public int? LineNumber { get; }
    public ModelfileParseException(string message, int? lineNumber = null) : base(message)
    {
        LineNumber = lineNumber;
    }
}

public class GpuMonitoringException : OllamaManagerException
{
    public GpuMonitoringException(string message) : base(message) { }
    public GpuMonitoringException(string message, Exception inner) : base(message, inner) { }
}

public class BackupException : OllamaManagerException
{
    public BackupException(string message) : base(message) { }
    public BackupException(string message, Exception inner) : base(message, inner) { }
}

public class DiscoveryException : OllamaManagerException
{
    public DiscoveryException(string message) : base(message) { }
    public DiscoveryException(string message, Exception inner) : base(message, inner) { }
}
