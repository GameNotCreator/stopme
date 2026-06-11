using System.Text.Json;
using System.Text.Json.Serialization;

namespace StopMe.NativeHost;

public static class MessageTypes
{
    public const string CheckUrl = "CHECK_URL";
    public const string CheckUrlResult = "CHECK_URL_RESULT";
    public const string GetStatus = "GET_STATUS";
    public const string Status = "STATUS";
    public const string GetActiveSessions = "GET_ACTIVE_SESSIONS";
    public const string ActiveSessions = "ACTIVE_SESSIONS";
    public const string Heartbeat = "HEARTBEAT";
    public const string HeartbeatAck = "HEARTBEAT_ACK";
    public const string Error = "ERROR";
}

public static class ErrorCodes
{
    public const string MalformedRequest = "malformed_request";
    public const string UnknownType = "unknown_type";
    public const string ServiceUnreachable = "service_unreachable";
    public const string Internal = "internal";
}

public sealed class CheckUrlRequest
{
    public string? Url { get; init; }
    public int? TabId { get; init; }
    public string? Browser { get; init; }
}

public sealed class CheckUrlResponse
{
    [JsonPropertyName("type")]
    public string Type => MessageTypes.CheckUrlResult;

    // Mirrored from the incoming request so the extension can correlate a
    // response to the right pending promise. Without it, a timed-out request
    // would shift the FIFO queue and the next response would resolve the
    // wrong tab's promise.
    public string? RequestId { get; init; }
    public required string Url { get; init; }
    public required bool Blocked { get; init; }
    public string? Reason { get; init; }
    public string? SessionName { get; init; }
    public Guid? SessionId { get; init; }
    public int? RemainingSeconds { get; init; }
    public string? MatchedRuleType { get; init; }
}

public sealed class StatusResponse
{
    [JsonPropertyName("type")]
    public string Type => MessageTypes.Status;

    public string? RequestId { get; init; }
    public required bool Connected { get; init; }
    public string? ServiceVersion { get; init; }
    public int ActiveSessionCount { get; init; }
    public int CompiledRuleCount { get; init; }
    public DateTime? NowUtc { get; init; }
    public string? Reason { get; init; }
}

public sealed class ActiveSessionsResponse
{
    [JsonPropertyName("type")]
    public string Type => MessageTypes.ActiveSessions;

    public string? RequestId { get; init; }
    public required IReadOnlyList<SessionPreview> Sessions { get; init; }
}

public sealed class SessionPreview
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Mode { get; init; }
    public required string LockPolicy { get; init; }
    public required DateTime EndAtUtc { get; init; }
    public required int RemainingSeconds { get; init; }
}

public sealed class HeartbeatRequest
{
    public string? Browser { get; init; }
    public string? ExtensionId { get; init; }
    public string? ExtensionVersion { get; init; }
}

public sealed class HeartbeatAck
{
    [JsonPropertyName("type")]
    public string Type => MessageTypes.HeartbeatAck;

    public string? RequestId { get; init; }
    public required bool Recorded { get; init; }
}

public sealed class ErrorResponse
{
    [JsonPropertyName("type")]
    public string Type => MessageTypes.Error;

    public string? RequestId { get; init; }
    public required string ErrorCode { get; init; }
    public required string ErrorMessage { get; init; }
    public string? Echo { get; init; }
}

internal static class JsonUtils
{
    public static string? ReadString(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    public static T? ReadAs<T>(this JsonElement element, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<T>(element.GetRawText(), options);
}
