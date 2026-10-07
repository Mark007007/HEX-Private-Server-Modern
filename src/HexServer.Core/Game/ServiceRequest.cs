namespace HexServer.Core.Game;

public readonly record struct ServiceRequest(
    int ServiceId,
    int MethodId,
    ulong? SessionId,
    ulong? PlayerId,
    ulong RequestId,
    byte[] Payload);
