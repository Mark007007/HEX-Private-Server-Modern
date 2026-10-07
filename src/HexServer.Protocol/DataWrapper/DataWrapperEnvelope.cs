namespace HexServer.Protocol.DataWrapper;

public sealed record DataWrapperEnvelope(
    long RequestId,
    int DataType,
    byte[] Bytes,
    Guid RequestHandlerSessionId,
    byte Compression);
