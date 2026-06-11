using System.Text.Json;
using System.Text.Json.Nodes;

namespace StopMe.LocalApi;

public sealed class LocalApiResponse
{
    public int Version { get; init; } = LocalApiProtocol.CurrentVersion;

    public bool Success { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public JsonNode? Payload { get; init; }

    public T? PayloadAs<T>(JsonSerializerOptions? options = null) =>
        Payload is null ? default : Payload.Deserialize<T>(options ?? JsonOptions.Default);

    public static LocalApiResponse Ok<T>(T payload) =>
        new()
        {
            Success = true,
            Payload = JsonSerializer.SerializeToNode(payload, JsonOptions.Default),
        };

    public static LocalApiResponse Ok() => new() { Success = true };

    public static LocalApiResponse Error(string code, string message) =>
        new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message,
        };
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
