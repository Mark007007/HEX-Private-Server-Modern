using HexServer.Core.Network;
using HexServer.Contracts;

namespace HexServer.Protocol.HConnect;

public static class HcpResponseFactory
{
    public static HcpFrame CreateServiceResponse(
        string serviceName,
        int serviceUid,
        ulong clientUid,
        string instance,
        long requestId,
        byte compression,
        int connectionHandle,
        ulong sessionId,
        long serverCounter,
        byte[] dataWrapperBytes)
    {
        var responseRequestId = requestId | 1L;

        var issuer =
            $"0.0.0.0.Service{serviceName}.{serviceUid}." +
            $"ServicePlayer.{clientUid}.{responseRequestId}";

        var header = HcpHeaderCodec.Encode(new Dictionary<string, object?>
        {
            ["issuer"] = issuer,
            ["target"] = $"Service{serviceName}",
            ["instance"] = instance,
            ["reqid"] = responseRequestId,
            ["c"] = compression,
            ["conh"] = connectionHandle,
            ["sid"] = sessionId,
            ["scnt"] = serverCounter
        });

        return new HcpFrame(header, dataWrapperBytes);
    }
}
