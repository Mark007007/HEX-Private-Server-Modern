using HexServer.Contracts;
using HexServer.Protocol.DataWrapper;

namespace HexServer.Protocol.HConnect;

public sealed record HcpServiceRequest(
    int ServiceId,
    int DataType,
    string Target,
    string Instance,
    long RequestId,
    byte Compression,
    int ConnectionHandle,
    ulong? SessionId,
    Guid RequestHandlerSessionId,
    long ClientCounter,
    byte[] Payload);

public static class HcpServiceMessage
{
    public static bool TryDecode(
        HcpMessage message,
        out HcpServiceRequest request)
    {
        request = default!;

        if (!message.Header.TryGetString("target", out var target) ||
            target is null)
        {
            return false;
        }

        if (!ServiceIds.TryFromTarget(
                target,
                out var serviceId))
        {
            return false;
        }

        if (!message.Header.Values.TryGetValue(
                "reqid",
                out var reqNode) ||
            !reqNode.TryGetInt64(out var requestId))
        {
            return false;
        }

        var instance =
            message.Header.TryGetString("instance", out var instanceValue)
                ? instanceValue ?? string.Empty
                : string.Empty;

        var compression = (byte)0;

        if (message.Header.Values.TryGetValue(
                "c",
                out var cNode) &&
            cNode.TryGetInt64(out var cValue) &&
            cValue is >= 0 and <= byte.MaxValue)
        {
            compression = (byte)cValue;
        }

        var connectionHandle =
            message.Header.Values.TryGetValue(
                "conh",
                out var conhNode) &&
            conhNode.TryGetInt64(out var conhValue) &&
            conhValue is >= int.MinValue and <= int.MaxValue
                ? (int)conhValue
                : 0;

        ulong? sessionId =
            message.Header.TryGetUInt64("sid", out var sidValue)
                ? sidValue
                : null;

        var clientCounter =
            message.Header.TryGetInt64("ccnt", out var ccntValue)
                ? ccntValue
                : 0;

        var wrapper = DataWrapperCodec.Decode(message.Body);

        var payload = DataWrapperCodec.DecodePayload(
            wrapper.Bytes,
            wrapper.Compression);

        request = new HcpServiceRequest(
            serviceId,
            wrapper.DataType,
            target,
            instance,
            requestId,
            compression,
            connectionHandle,
            sessionId,
            wrapper.RequestHandlerSessionId,
            clientCounter,
            payload);

        return true;
    }
}
