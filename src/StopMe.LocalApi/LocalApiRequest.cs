using System.Text.Json;
using System.Text.Json.Nodes;

namespace StopMe.LocalApi;

public sealed class LocalApiRequest
{
    public int Version { get; init; } = LocalApiProtocol.CurrentVersion;

    public required string Op { get; init; }

    public string? Token { get; init; }

    public JsonNode? Payload { get; init; }

    public T? PayloadAs<T>(JsonSerializerOptions? options = null) =>
        Payload is null ? default : Payload.Deserialize<T>(options ?? JsonOptions.Default);

    public static LocalApiRequest For<T>(string op, T payload, string? token = null) =>
        new()
        {
            Op = op,
            Token = token,
            Payload = JsonSerializer.SerializeToNode(payload, JsonOptions.Default),
        };

    public static LocalApiRequest For(string op, string? token = null) =>
        new() { Op = op, Token = token };
}
