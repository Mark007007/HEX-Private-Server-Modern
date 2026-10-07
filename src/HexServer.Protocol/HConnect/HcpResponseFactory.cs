using HexServer.Contracts;
using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public static class HcpResponseFactory
{
    public static HcpFrame CreateServiceResponse(
        string serviceTarget,
        int serviceUid,
        ulong clientUid,
        string instance,
        long requestId,
        byte compression,
        int connectionHandle,
        ulong sessionId,
        long serverCounter,
        Guid requestHandlerSessionId,
        ReadOnlySpan<byte> uncompressedResponsePayload)
    {
        var responseRequestId = requestId | 1L;
        var bodyPayload = DataWrapper.DataWrapperCodec.EncodePayload(
            uncompressedResponsePayload,
            compression);

        var wrapper = DataWrapper.DataWrapperCodec.Encode(
            responseRequestId,
            serviceUid == ServiceIds.GameSession ? requestId switch
            {
                _ => requestId >= 0 ? 0 : 0
            } : 0,
            bodyPayload,
            compression,
            requestHandlerSessionId);

        var issuer =
            $"0.0.0.0.{serviceTarget}.{serviceUid}." +
            $"ServicePlayer.{clientUid}.{responseRequestId}";

        var header = HcpHeaderCodec.Encode(new Dictionary<string, object?>
        {
            ["issuer"] = issuer,
            ["target"] = serviceTarget,
            ["instance"] = instance,
            ["reqid"] = responseRequestId,
            ["c"] = compression,
            ["conh"] = connectionHandle,
            ["sid"] = sessionId,
            ["scnt"] = serverCounter
        });

        return new HcpFrame(header, wrapper);
    }
}
