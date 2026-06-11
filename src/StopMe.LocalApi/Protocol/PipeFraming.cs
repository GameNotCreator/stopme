using System.Buffers.Binary;
using System.Text.Json;

using StopMe.LocalApi;

namespace StopMe.LocalApi.Protocol;

public static class PipeFraming
{
    public static async Task WriteAsync<T>(Stream stream, T payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions.Default);
        if (bytes.Length > LocalApiProtocol.MaxMessageBytes)
            throw new InvalidOperationException($"Payload of {bytes.Length} bytes exceeds limit.");

        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[4];
        if (!await ReadExactlyAsync(stream, header, ct).ConfigureAwait(false))
            return default;

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > LocalApiProtocol.MaxMessageBytes)
            throw new InvalidOperationException($"Invalid frame length {length}.");

        var buffer = new byte[length];
        if (!await ReadExactlyAsync(stream, buffer, ct).ConfigureAwait(false))
            return default;

        return JsonSerializer.Deserialize<T>(buffer, JsonOptions.Default);
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
