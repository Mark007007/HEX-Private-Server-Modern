using HexServer.Contracts;
using HexServer.Protocol.Contracts;
using HexServer.Protocol.HConnect;
using HexServer.Protocol.Services;

namespace HexServer.Server;

public sealed class GameSessionServiceHandler : IServiceHandler
{
    private readonly GameSessionRegistry _sessions;

    public GameSessionServiceHandler(GameSessionRegistry sessions)
        => _sessions = sessions;

    public ValueTask<ServiceResponse> HandleAsync(
        HcpServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return request.DataType switch
        {
            LoadBalancerDataTypes.StartSession =>
                ValueTask.FromResult(HandleStartSession(request)),

            GameSessionMethodIds.FindSession =>
                ValueTask.FromResult(HandleFindSession(request)),

            GameSessionMethodIds.JoinSession =>
                ValueTask.FromResult(HandleJoinSession(request)),

            _ => throw new NotSupportedException(
                $"GameSession method {request.MethodId} is not implemented yet.")
        };
    }

    private ServiceResponse HandleStartSession(HcpServiceRequest request)
    {
        var value = GameSessionContractCodec.DecodeStartSession(
            request.Payload);

        var existing = _sessions.FindByName(value.SessionName);

        var session = existing ?? _sessions.Create(
            value.SessionName,
            value.MinPlayers,
            value.MaxPlayers,
            value.PlayerId.Value);

        var response = new StartSessionResponse(
            value.RequestHandlerSessionId,
            value.OriginClusterHash,
            value.PlayerId,
            Success: true,
            session.SessionId,
            session.Name,
            session.MinimumPlayers,
            session.MaximumPlayers,
            JoinInsteadOfReconnect: false);

        return new ServiceResponse(
            request.MethodId,
            value.PlayerId.Value,
            GameSessionContractCodec.EncodeStartSessionResponse(response),
            request.Compression);
    }

    private ServiceResponse HandleFindSession(HcpServiceRequest request)
    {
        var value = GameSessionContractCodec.DecodeFindSession(
            request.Payload);

        var session = _sessions.FindByName(value.SessionName);

        var payload = session is null
            ? GameSessionContractCodec.EncodeFindSessionResponse(
                value.RequestHandlerSessionId,
                value.OriginClusterHash,
                value.PlayerId.Value,
                Success: false,
                sessionId: 0,
                sessionName: value.SessionName,
                minimumPlayerCount: 0,
                maximumPlayerCount: 0)
            : GameSessionContractCodec.EncodeFindSessionResponse(
                value.RequestHandlerSessionId,
                value.OriginClusterHash,
                value.PlayerId.Value,
                Success: true,
                session.SessionId.Value,
                session.Name,
                session.MinimumPlayers,
                session.MaximumPlayers);

        return new ServiceResponse(
            request.MethodId,
            value.PlayerId.Value,
            payload,
            request.Compression);
    }

    private ServiceResponse HandleJoinSession(HcpServiceRequest request)
    {
        var value = GameSessionContractCodec.DecodeJoinSession(
            request.Payload);

        var sessionExists =
            _sessions.TryGet(value.SessionId, out var session) &&
            session is not null;

        var success =
            sessionExists &&
            _sessions.TryAddPlayer(
                value.SessionId,
                value.PlayerId.Value,
                value.PlayerPosition);

        var response = new JoinSessionResponse(
            value.RequestHandlerSessionId,
            value.OriginClusterHash,
            value.PlayerId,
            success,
            session?.SessionId ?? value.SessionId,
            session?.Name ?? string.Empty,
            session?.MinimumPlayers ?? 0,
            session?.MaximumPlayers ?? 0,
            JoinInsteadOfReconnect: false,
            session?.Players ?? Array.Empty<(ulong, int)>());

        return new ServiceResponse(
            request.MethodId,
            value.PlayerId.Value,
            GameSessionContractCodec.EncodeJoinSessionResponse(response),
            request.Compression);
    }
}
