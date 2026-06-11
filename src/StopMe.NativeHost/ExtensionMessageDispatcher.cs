using System.Text.Json;
using Microsoft.Extensions.Logging;
using StopMe.LocalApi.Dtos;

namespace StopMe.NativeHost;

public sealed class ExtensionMessageDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IServiceBridge _bridge;
    private readonly ILogger<ExtensionMessageDispatcher> _logger;

    public ExtensionMessageDispatcher(IServiceBridge bridge, ILogger<ExtensionMessageDispatcher> logger)
    {
        _bridge = bridge;
        _logger = logger;
    }

    public async Task<object> DispatchAsync(JsonDocument request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RootElement.ValueKind != JsonValueKind.Object)
            return Error(ErrorCodes.MalformedRequest, "Expected a JSON object.");

        var type = request.RootElement.ReadString("type");
        if (string.IsNullOrEmpty(type))
            return Error(ErrorCodes.MalformedRequest, "Missing 'type' field.");

        try
        {
            switch (type)
            {
                case MessageTypes.CheckUrl:
                    return await HandleCheckUrlAsync(request.RootElement, ct).ConfigureAwait(false);
                case MessageTypes.GetStatus:
                    return await HandleStatusAsync(ct).ConfigureAwait(false);
                case MessageTypes.GetActiveSessions:
                    return await HandleActiveSessionsAsync(ct).ConfigureAwait(false);
                case MessageTypes.Heartbeat:
                    return await HandleHeartbeatAsync(request.RootElement, ct).ConfigureAwait(false);
                default:
                    return Error(ErrorCodes.UnknownType, $"Unknown message type '{type}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatch failed for type {Type}", type);
            return Error(ErrorCodes.Internal, ex.Message);
        }
    }

    private async Task<object> HandleCheckUrlAsync(JsonElement root, CancellationToken ct)
    {
        var dto = root.ReadAs<CheckUrlRequest>(JsonOptions);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Url))
            return Error(ErrorCodes.MalformedRequest, "Missing 'url'.");

        var decision = await _bridge.EvaluateUrlAsync(dto.Url, dto.Browser, ct).ConfigureAwait(false);
        if (decision is null)
            return Error(ErrorCodes.ServiceUnreachable, _bridge.LastError ?? "Service did not respond.");

        return new CheckUrlResponse
        {
            Url = dto.Url,
            Blocked = decision.Blocked,
            Reason = decision.Reason,
            SessionName = decision.SessionName,
            SessionId = decision.SessionId,
            RemainingSeconds = decision.RemainingSeconds,
            MatchedRuleType = decision.MatchedRuleType,
        };
    }

    private async Task<object> HandleStatusAsync(CancellationToken ct)
    {
        var status = await _bridge.GetStatusAsync(ct).ConfigureAwait(false);
        if (status is null)
        {
            return new StatusResponse
            {
                Connected = false,
                Reason = _bridge.LastError ?? "Service unreachable.",
                ActiveSessionCount = 0,
                CompiledRuleCount = 0,
            };
        }
        return new StatusResponse
        {
            Connected = true,
            ServiceVersion = status.ServiceVersion,
            ActiveSessionCount = status.ActiveSessionCount,
            CompiledRuleCount = status.CompiledRuleCount,
            NowUtc = status.NowUtc,
        };
    }

    private async Task<object> HandleActiveSessionsAsync(CancellationToken ct)
    {
        var sessions = await _bridge.GetActiveSessionsAsync(ct).ConfigureAwait(false);
        return new ActiveSessionsResponse
        {
            Sessions = sessions.Select(s => new SessionPreview
            {
                Id = s.SessionId,
                Name = s.Name,
                Mode = s.Mode,
                LockPolicy = s.LockPolicy,
                EndAtUtc = s.EndAtUtc,
                RemainingSeconds = s.RemainingSeconds,
            }).ToArray(),
        };
    }

    private async Task<object> HandleHeartbeatAsync(JsonElement root, CancellationToken ct)
    {
        var dto = root.ReadAs<HeartbeatRequest>(JsonOptions);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Browser) || string.IsNullOrWhiteSpace(dto.ExtensionId))
            return Error(ErrorCodes.MalformedRequest, "Missing browser/extensionId.");

        var recorded = await _bridge.SendHeartbeatAsync(new ExtensionHeartbeatRequest
        {
            Browser = dto.Browser,
            ExtensionId = dto.ExtensionId,
            ExtensionVersion = dto.ExtensionVersion,
        }, ct).ConfigureAwait(false);

        if (!recorded)
            return Error(ErrorCodes.ServiceUnreachable, _bridge.LastError ?? "Heartbeat not recorded.");

        return new HeartbeatAck { Recorded = true };
    }

    private static ErrorResponse Error(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
