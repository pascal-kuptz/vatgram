using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Vatgram.Shared;

public static class IpcCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task WriteAsync(Stream stream, IpcMessage message, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, Options);
        var lengthPrefix = BitConverter.GetBytes(json.Length);
        await stream.WriteAsync(lengthPrefix, 0, 4, ct).ConfigureAwait(false);
        await stream.WriteAsync(json, 0, json.Length, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<IpcMessage?> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        var lengthBuf = new byte[4];
        if (!await ReadExactAsync(stream, lengthBuf, 4, ct).ConfigureAwait(false))
            return null;
        var length = BitConverter.ToInt32(lengthBuf, 0);
        if (length <= 0 || length > 1_000_000) return null;
        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, length, ct).ConfigureAwait(false))
            return null;
        try { return JsonSerializer.Deserialize<IpcMessage>(payload, Options); }
        catch (JsonException) { return UnknownMessage.Instance; } // unknown discriminator from a newer/older peer; skip
    }

    /// <summary>Sentinel returned when the JSON payload is well-formed but its discriminator
    /// is unknown to this side (version mismatch). Receivers should ignore this and continue reading.</summary>
    public sealed record UnknownMessage : IpcMessage
    {
        public static readonly UnknownMessage Instance = new();
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buffer, read, count - read, ct).ConfigureAwait(false);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }
}
