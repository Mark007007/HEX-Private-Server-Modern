using HexServer.Contracts;
using HexServer.Core.Network;
using HexServer.Protocol.DataWrapper;

namespace HexServer.Protocol.HConnect;

public static class HcpResponseFactory
{
    public static HcpFrame CreateServiceResponse(
        string serviceTarget,
        int serviceUid,
        ulong clientUid,
        string instance,
        long requestId,
        int dataType,
        byte compression,
        int connectionHandle,
        ulong sessionId,
        long serverCounter,
        Guid requestHandlerSessionId,
        ReadOnlySpan<byte> uncompressedResponsePayload)
    {
        var responseRequestId = requestId | 1L;
        var bodyPayload = DataWrapperCodec.EncodePayload(
            uncompressedResponsePayload,
            compression);

        var wrapper = DataWrapperCodec.Encode(
            responseRequestId,
            dataType,
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
