using System.Buffers.Binary;
using System.Text.Json;

namespace StopMe.NativeHost;

// Chrome / Edge Native Messaging frame format: 4-byte little-endian uint32 length
// followed by UTF-8 JSON. Max 1 MiB inbound (browser → host), 64 MiB outbound.
public sealed class NativeMessagingChannel
{
    public const int MaxInboundBytes = 1 * 1024 * 1024;
    public const int MaxOutboundBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Stream _stdin;
    private readonly Stream _stdout;

    public NativeMessagingChannel(Stream stdin, Stream stdout)
    {
        _stdin = stdin;
        _stdout = stdout;
    }

    public async Task<JsonDocument?> ReadAsync(CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactlyAsync(_stdin, header, ct).ConfigureAwait(false))
            return null;

        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length <= 0 || length > MaxInboundBytes)
            throw new InvalidOperationException($"Invalid inbound frame length {length}.");

        var buffer = new byte[length];
        if (!await ReadExactlyAsync(_stdin, buffer, ct).ConfigureAwait(false))
            return null;

        return JsonDocument.Parse(buffer);
    }

    public async Task WriteAsync<T>(T payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        if (bytes.Length > MaxOutboundBytes)
            throw new InvalidOperationException($"Outbound payload of {bytes.Length} bytes exceeds limit.");

        var header = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)bytes.Length);

        await _stdout.WriteAsync(header, ct).ConfigureAwait(false);
        await _stdout.WriteAsync(bytes, ct).ConfigureAwait(false);
        await _stdout.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer[read..], ct).ConfigureAwait(false);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }
}
