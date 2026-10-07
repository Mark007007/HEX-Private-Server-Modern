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

    public async ValueTask<ServiceResponse> HandleAsync(
        HcpServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return request.MethodId switch
        {
            GameSessionMethodIds.StartSession =>
                HandleStartSession(request),
            GameSessionMethodIds.FindSession =>
                HandleFindSession(request),
            GameSessionMethodIds.JoinSession =>
                HandleJoinSession(request),
            _ => throw new NotSupportedException(
                $"GameSession method {request.MethodId} is not implemented yet.")
        };
    }

    private ServiceResponse HandleStartSession(HcpServiceRequest request)
    {
        var value = GameSessionContractCodec.DecodeStartSession(request.Payload);

        var existing = _sessions.FindByName(value.SessionName);
        var session = existing is null
            ? _sessions.Create(
                value.SessionName,
                value.MinPlayers,
                value.MaxPlayers,
                value.PlayerId.Value)
            : existing;

        var response = new StartSessionResponse(
            value.RequestHandlerSessionId,
            value.OriginClusterHash,
            value.PlayerId,
            true,
            session.SessionId,
            session.Name,
            session.MinimumPlayers,
            session.MaximumPlayers,
            false);

        return new ServiceResponse(
            request.MethodId,
            value.PlayerId.Value,
            GameSessionContractCodec.EncodeStartSessionResponse(response),
            request.Compression);
    }

    private ServiceResponse HandleFindSession(HcpServiceRequest request)
    {
        // FindSession's full request contract is intentionally decoded through the
        // recovered Object graph in the next pass. For now use instance/name routing
        // when the caller supplied an instance equal to a known session name.
        var session = _sessions.FindByName(request.Instance);

        if (session is null)
        {
            var error = GameSessionContractCodec.EncodeFindSessionResponse(
                request.RequestHandlerSessionId,
                0,
                0,
                false,
                0,
                request.Instance,
                0,
                0);

            return new ServiceResponse(
                request.MethodId,
                0,
                error,
                request.Compression);
        }

        var response = GameSessionContractCodec.EncodeFindSessionResponse(
            request.RequestHandlerSessionId,
            0,
            session.Players.FirstOrDefault().PlayerId,
            true,
            session.SessionId.Value,
            session.Name,
            session.MinimumPlayers,
            session.MaximumPlayers);

        return new ServiceResponse(
            request.MethodId,
            session.Players.FirstOrDefault().PlayerId,
            response,
            request.Compression);
    }

    private ServiceResponse HandleJoinSession(HcpServiceRequest request)
    {
        var value = GameSessionContractCodec.DecodeJoinSession(request.Payload);

        var success = _sessions.TryAddPlayer(
            value.SessionId,
            value.PlayerId.Value,
            value.PlayerPosition);

        if (!_sessions.TryGet(value.SessionId, out var session) || session is null)
            success = false;

        var response = new JoinSessionResponse(
            value.RequestHandlerSessionId,
            value.OriginClusterHash,
            value.PlayerId,
            success,
            session?.SessionId ?? value.SessionId,
            session?.Name ?? string.Empty,
            session?.MinimumPlayers ?? 0,
            session?.MaximumPlayers ?? 0,
            false,
            session?.Players ?? Array.Empty<(ulong, int)>());

        return new ServiceResponse(
            request.MethodId,
            value.PlayerId.Value,
            GameSessionContractCodec.EncodeJoinSessionResponse(response),
            request.Compression);
    }
}
