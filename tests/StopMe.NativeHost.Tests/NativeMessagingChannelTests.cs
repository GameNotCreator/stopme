using System.Text.Json;
using FluentAssertions;
using StopMe.NativeHost;
using Xunit;

namespace StopMe.NativeHost.Tests;

public class NativeMessagingChannelTests
{
    [Fact]
    public async Task Write_then_read_roundtrips_payload()
    {
        var stream = new MemoryStream();
        var channelWrite = new NativeMessagingChannel(Stream.Null, stream);
        await channelWrite.WriteAsync(new { type = "PING", payload = "hello" }, default);

        stream.Position = 0;
        var channelRead = new NativeMessagingChannel(stream, Stream.Null);
        using var doc = await channelRead.ReadAsync(default);

        doc.Should().NotBeNull();
        doc!.RootElement.GetProperty("type").GetString().Should().Be("PING");
        doc.RootElement.GetProperty("payload").GetString().Should().Be("hello");
    }

    [Fact]
    public async Task Read_returns_null_on_eof()
    {
        var empty = new MemoryStream();
        var channel = new NativeMessagingChannel(empty, Stream.Null);

        var result = await channel.ReadAsync(default);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Read_rejects_oversized_frame()
    {
        var stream = new MemoryStream();
        var length = NativeMessagingChannel.MaxInboundBytes + 1;
        stream.Write(BitConverter.GetBytes((uint)length));
        stream.Position = 0;

        var channel = new NativeMessagingChannel(stream, Stream.Null);
        Func<Task> act = async () => await channel.ReadAsync(default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Multiple_frames_roundtrip_in_order()
    {
        var stream = new MemoryStream();
        var writer = new NativeMessagingChannel(Stream.Null, stream);
        await writer.WriteAsync(new { type = "A" }, default);
        await writer.WriteAsync(new { type = "B" }, default);

        stream.Position = 0;
        var reader = new NativeMessagingChannel(stream, Stream.Null);

        using var first = await reader.ReadAsync(default);
        using var second = await reader.ReadAsync(default);

        first!.RootElement.GetProperty("type").GetString().Should().Be("A");
        second!.RootElement.GetProperty("type").GetString().Should().Be("B");
    }
}
