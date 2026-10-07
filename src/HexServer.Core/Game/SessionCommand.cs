namespace HexServer.Core.Game;

public abstract record SessionCommand;

public sealed record HandshakeCommand(string Target, ulong? SessionId) : SessionCommand;

public sealed record ServiceRequestCommand(
    int ServiceId,
    int MethodId,
    ulong PlayerId,
    byte[] Payload) : SessionCommand;
