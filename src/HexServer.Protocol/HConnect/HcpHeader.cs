using System.Text.Json;

namespace HexServer.Protocol.HConnect;

public sealed record HcpHeader(IReadOnlyDictionary<string, JsonElement> Values)
{
    public bool TryGetString(string key, out string? value)
    {
        if (Values.TryGetValue(key, out var node) && node.ValueKind == JsonValueKind.String)
        {
            value = node.GetString();
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetUInt64(string key, out ulong value)
    {
        if (Values.TryGetValue(key, out var node) &&
            node.ValueKind == JsonValueKind.Number &&
            node.TryGetUInt64(out value))
            return true;

        value = 0;
        return false;
    }

    public bool TryGetInt64(string key, out long value)
    {
        if (Values.TryGetValue(key, out var node) &&
            node.ValueKind == JsonValueKind.Number &&
            node.TryGetInt64(out value))
            return true;

        value = 0;
        return false;
    }
}

public static class HcpHeaderCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public static HcpHeader Decode(ReadOnlySpan<byte> utf8)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(utf8, Options)
                     ?? throw new InvalidDataException("HCP header JSON is null.");

        return new HcpHeader(values);
    }

    public static byte[] Encode(IReadOnlyDictionary<string, object?> values)
        => JsonSerializer.SerializeToUtf8Bytes(values, Options);
}
