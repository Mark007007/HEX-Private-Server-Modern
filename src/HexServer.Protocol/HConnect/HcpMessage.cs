using System.Text.Json;
using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public sealed record HcpMessage(
    HcpHeader Header,
    byte[] Body)
{
    public bool TryGetTarget(out string? target) => Header.TryGetString("target", out target);
    public bool TryGetIssuer(out string? issuer) => Header.TryGetString("issuer", out issuer);
    public bool TryGetSessionId(out ulong sid) => Header.TryGetUInt64("sid", out sid);
    public bool TryGetClientCounter(out long ccnt) => Header.TryGetInt64("ccnt", out ccnt);
    public bool TryGetServerCounter(out long scnt) => Header.TryGetInt64("scnt", out scnt);

    public HcpFrame ToFrame()
    {
        var headerValues = Header.Values.ToDictionary(
            static x => x.Key,
            static x => (object?)JsonElementToObject(x.Value));

        return new HcpFrame(HcpHeaderCodec.Encode(headerValues), Body);
    }

    private static object? JsonElementToObject(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var i64) => i64,
        JsonValueKind.Number when value.TryGetDouble(out var dbl) => dbl,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value.Clone()
    };
}
