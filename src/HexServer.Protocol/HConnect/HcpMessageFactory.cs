using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public static class HcpMessageFactory
{
    public static HcpFrame CreateSession(
        ulong sessionId,
        long serverCounter,
        long clientCounter,
        string version = "1")
    {
        var header = HcpHeaderCodec.Encode(new Dictionary<string, object?>
        {
            ["issuer"] = "Session",
            ["target"] = "create",
            ["sid"] = sessionId,
            ["scnt"] = serverCounter,
            ["ccnt"] = clientCounter,
            ["version"] = version,
            ["time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        return new HcpFrame(header, Array.Empty<byte>());
    }
}
