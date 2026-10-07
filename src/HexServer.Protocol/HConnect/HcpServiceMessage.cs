using System.Text.Json;
using HexServer.Core.Game;
using HexServer.Contracts;
using HexServer.Protocol.DataWrapper;

namespace HexServer.Protocol.HConnect;

public sealed record HcpServiceRequest(
    int ServiceId,
    int MethodId,
    string Target,
    string Instance,
    long RequestId,
    byte Compression,
    int ConnectionHandle,
    ulong? SessionId,
    byte[] Payload);

public static class HcpServiceMessage
{
    public static bool TryDecode(HcpMessage message, out HcpServiceRequest request)
    {
        request = default!;

        if (!message.TryGetString("target", out var target) ||
            target is null ||
            !ServiceIds.TryFromTarget(target, out var serviceId))
            return false;

        if (!message.Header.Values.TryGetValue("reqid", out var reqNode) ||
            !reqNode.TryGetInt64(out var requestId))
            return false;

        var instance = message.Header.TryGetString("instance", out var instanceValue)
            ? instanceValue ?? string.Empty
            : string.Empty;

        var compression = message.Header.Values.TryGetValue("c", out var cNode) &&
                          cNode.TryGetByte(out var c)
            ? c
            : (byte)0;

        var conh = message.Header.Values.TryGetValue("conh", out var conhNode) &&
                   conhNode.TryGetInt32(out var handle)
            ? handle
            : 0;

        ulong? sid = message.TryGetSessionId(out var sidValue) ? sidValue : null;

        var wrapper = DataWrapperCodec.Decode(message.Body);
        var payload = DataWrapperCodec.DecodePayload(wrapper.Bytes, wrapper.Compression);

        request = new HcpServiceRequest(
            serviceId,
            wrapper.DataType,
            target,
            instance,
            requestId,
            compression,
            conh,
            sid,
            payload);

        return true;
    }
}
