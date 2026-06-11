using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StopMe.LocalApi.Dtos;
using StopMe.NativeHost;
using StopMe.NativeHost.Tests.Fakes;
using Xunit;

namespace StopMe.NativeHost.Tests;

public class ExtensionMessageDispatcherTests
{
    [Fact]
    public async Task CHECK_URL_returns_decision_from_bridge()
    {
        var bridge = new FakeServiceBridge
        {
            UrlResponseToReturn = new EvaluateUrlResponse
            {
                Blocked = true,
                Reason = "youtube",
                SessionName = "Focus",
                RemainingSeconds = 1200,
                MatchedRuleType = "Domain",
            },
        };
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { type = "CHECK_URL", url = "https://youtube.com/" }), default);

        resp.Should().BeOfType<CheckUrlResponse>();
        var dto = (CheckUrlResponse)resp;
        dto.Blocked.Should().BeTrue();
        dto.SessionName.Should().Be("Focus");
        dto.RemainingSeconds.Should().Be(1200);
        bridge.EvaluatedUrls.Should().ContainSingle().Which.Should().Be("https://youtube.com/");
    }

    [Fact]
    public async Task CHECK_URL_returns_error_when_service_unreachable()
    {
        var bridge = new FakeServiceBridge { Connected = false, LastError = "service down" };
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { type = "CHECK_URL", url = "https://youtube.com/" }), default);

        resp.Should().BeOfType<ErrorResponse>();
        var err = (ErrorResponse)resp;
        err.ErrorCode.Should().Be(ErrorCodes.ServiceUnreachable);
        err.ErrorMessage.Should().Be("service down");
    }

    [Fact]
    public async Task GET_STATUS_returns_connected_payload_when_reachable()
    {
        var bridge = new FakeServiceBridge
        {
            StatusToReturn = new StatusDto
            {
                ServiceVersion = "0.1.0",
                NowUtc = new DateTime(2026, 6, 9, 9, 0, 0, DateTimeKind.Utc),
                LastTickUtc = new DateTime(2026, 6, 9, 9, 0, 0, DateTimeKind.Utc),
                BootRestoreCompleted = true,
                ActiveSessionCount = 2,
                CompiledRuleCount = 50,
            },
        };
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { type = "GET_STATUS" }), default);

        resp.Should().BeOfType<StatusResponse>();
        var dto = (StatusResponse)resp;
        dto.Connected.Should().BeTrue();
        dto.ServiceVersion.Should().Be("0.1.0");
        dto.ActiveSessionCount.Should().Be(2);
        dto.CompiledRuleCount.Should().Be(50);
    }

    [Fact]
    public async Task GET_STATUS_returns_disconnected_payload_with_reason()
    {
        var bridge = new FakeServiceBridge { Connected = false, LastError = "no service" };
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { type = "GET_STATUS" }), default);

        resp.Should().BeOfType<StatusResponse>();
        var dto = (StatusResponse)resp;
        dto.Connected.Should().BeFalse();
        dto.Reason.Should().Be("no service");
    }

    [Fact]
    public async Task GET_ACTIVE_SESSIONS_projects_dto_to_preview()
    {
        var bridge = new FakeServiceBridge();
        bridge.ActiveSessionsToReturn.Add(new ActiveSessionDto
        {
            SessionId = Guid.NewGuid(),
            Name = "Focus",
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddMinutes(30),
            Mode = "Strict",
            LockPolicy = "TimeLocked",
            RemainingSeconds = 1800,
            BlocklistIds = Array.Empty<Guid>(),
        });
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { type = "GET_ACTIVE_SESSIONS" }), default);

        resp.Should().BeOfType<ActiveSessionsResponse>();
        var dto = (ActiveSessionsResponse)resp;
        dto.Sessions.Should().ContainSingle();
        dto.Sessions[0].Mode.Should().Be("Strict");
        dto.Sessions[0].LockPolicy.Should().Be("TimeLocked");
        dto.Sessions[0].RemainingSeconds.Should().Be(1800);
    }

    [Fact]
    public async Task Unknown_type_returns_error()
    {
        var bridge = new FakeServiceBridge();
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { type = "WAT" }), default);

        resp.Should().BeOfType<ErrorResponse>();
        ((ErrorResponse)resp).ErrorCode.Should().Be(ErrorCodes.UnknownType);
    }

    [Fact]
    public async Task Missing_type_returns_malformed_error()
    {
        var bridge = new FakeServiceBridge();
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { foo = "bar" }), default);

        resp.Should().BeOfType<ErrorResponse>();
        ((ErrorResponse)resp).ErrorCode.Should().Be(ErrorCodes.MalformedRequest);
    }

    [Fact]
    public async Task CHECK_URL_without_url_returns_malformed_error()
    {
        var bridge = new FakeServiceBridge();
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var resp = await d.DispatchAsync(Wrap(new { type = "CHECK_URL" }), default);

        resp.Should().BeOfType<ErrorResponse>();
        ((ErrorResponse)resp).ErrorCode.Should().Be(ErrorCodes.MalformedRequest);
    }

    private static JsonDocument Wrap(object o) =>
        JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(o));
}
