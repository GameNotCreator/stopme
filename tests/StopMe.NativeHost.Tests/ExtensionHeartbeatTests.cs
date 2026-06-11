using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StopMe.NativeHost;
using StopMe.NativeHost.Tests.Fakes;
using Xunit;

namespace StopMe.NativeHost.Tests;

public class ExtensionHeartbeatTests
{
    [Fact]
    public async Task HEARTBEAT_forwards_to_bridge_and_returns_ack()
    {
        var bridge = new FakeServiceBridge();
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "HEARTBEAT",
            browser = "chrome",
            extensionId = "abc",
            extensionVersion = "0.1.0",
        }));
        var resp = await d.DispatchAsync(doc, default);

        resp.Should().BeOfType<HeartbeatAck>();
        ((HeartbeatAck)resp).Recorded.Should().BeTrue();
        bridge.Heartbeats.Should().ContainSingle();
        bridge.Heartbeats[0].Browser.Should().Be("chrome");
        bridge.Heartbeats[0].ExtensionVersion.Should().Be("0.1.0");
    }

    [Fact]
    public async Task HEARTBEAT_missing_browser_returns_malformed_error()
    {
        var bridge = new FakeServiceBridge();
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new { type = "HEARTBEAT" }));
        var resp = await d.DispatchAsync(doc, default);

        resp.Should().BeOfType<ErrorResponse>();
        ((ErrorResponse)resp).ErrorCode.Should().Be(ErrorCodes.MalformedRequest);
    }

    [Fact]
    public async Task HEARTBEAT_returns_service_unreachable_when_bridge_fails()
    {
        var bridge = new FakeServiceBridge { HeartbeatResult = false, LastError = "no service" };
        var d = new ExtensionMessageDispatcher(bridge, NullLogger<ExtensionMessageDispatcher>.Instance);

        var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "HEARTBEAT",
            browser = "chrome",
            extensionId = "abc",
        }));
        var resp = await d.DispatchAsync(doc, default);

        resp.Should().BeOfType<ErrorResponse>();
        ((ErrorResponse)resp).ErrorCode.Should().Be(ErrorCodes.ServiceUnreachable);
    }
}
