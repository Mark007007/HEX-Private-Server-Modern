namespace HexServer.Contracts;

public readonly record struct HexUid(ulong Value);

public readonly record struct HexResourceId(Guid Value);

public sealed record HexRemotePlayer(Guid Id);

public sealed record StartSessionRequest(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid PlayerId,
    string SessionName,
    int MinPlayers,
    int MaxPlayers,
    int AIPlayers,
    IReadOnlyList<HexRemotePlayer> ParticipatingPlayers,
    int SessionFlags,
    bool Observable,
    HexResourceId FilterId,
    ulong TestDeckId);

public sealed record StartSessionResponse(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid RoutingPlayerId,
    bool Success,
    HexUid SessionId,
    string SessionName,
    int MinimumPlayerCount,
    int MaximumPlayerCount,
    bool JoinInsteadOfReconnect);

public sealed record FindSessionRequest(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid PlayerId,
    string SessionName);

public sealed record JoinSessionRequest(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid PlayerId,
    HexUid SessionId,
    ulong DeckId,
    HexResourceId DeckTemplateId,
    int PlayerPosition,
    ulong ChampionId,
    IReadOnlyList<int> SelfTurnPhases,
    IReadOnlyList<int> OpponentTurnPhases);

public sealed record JoinSessionResponse(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid RoutingPlayerId,
    bool Success,
    HexUid SessionId,
    string SessionName,
    int MinimumPlayerCount,
    int MaximumPlayerCount,
    bool JoinInsteadOfReconnect,
    IReadOnlyList<(ulong PlayerId, int Position)> SessionPlayers);

public sealed record ReadyForGameSetupRequest(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid PlayerId,
    HexUid SessionId,
    IReadOnlyList<int> SelfTurnPhases,
    IReadOnlyList<int> OpponentTurnPhases);

public sealed record ReadyForGameSetupResponse(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    bool Success,
    HexUid SessionId);

public sealed record ReadyForGameEventsRequest(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid PlayerId,
    HexUid SessionId);

public sealed record ReadyForGameEventsResponse(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    int Result);

public sealed record ReadyToStartGameRequest(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid PlayerId,
    bool IsReady);

public sealed record ReadyToStartGameResponse(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    int Result);

public sealed record PlayerTransactionRequest(
    Guid RequestHandlerSessionId,
    int OriginClusterHash,
    HexUid PlayerId,
    byte[] TransactionObjFmt);

public sealed record SessionInfo(
    HexUid SessionId,
    string Name,
    int MinimumPlayers,
    int MaximumPlayers,
    IReadOnlyList<(ulong PlayerId, int Position)> Players);
