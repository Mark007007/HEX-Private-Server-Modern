using HexServer.Contracts;
using HexServer.Protocol.Contracts;
using HexServer.Protocol.HConnect;
using HexServer.Protocol.Services;

namespace HexServer.Server;

public sealed class LoadBalancerServiceHandler : IServiceHandler
{
    private readonly GameSessionRegistry _sessions;

    public LoadBalancerServiceHandler(GameSessionRegistry sessions)
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

            LoadBalancerDataTypes.FindSession =>
                ValueTask.FromResult(HandleFindSession(request)),

            LoadBalancerDataTypes.JoinSession =>
                ValueTask.FromResult(HandleJoinSession(request)),

            _ => throw new NotSupportedException(
                $"LoadBalancer data type {request.DataType} is not implemented yet.")
        };
    }

    private ServiceResponse HandleStartSession(
        HcpServiceRequest request)
    {
        var value = GameSessionContractCodec.DecodeStartSession(
            request.Payload);

        var session = _sessions.FindByName(value.SessionName)
            ?? _sessions.Create(
                value.SessionName,
                value.MinPlayers,
                value.MaxPlayers,
                value.PlayerId.Value);

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
            request.DataType,
            value.PlayerId.Value,
            GameSessionContractCodec.EncodeStartSessionResponse(response),
            request.Compression);
    }

    private ServiceResponse HandleFindSession(
        HcpServiceRequest request)
    {
        var value = GameSessionContractCodec.DecodeFindSession(
            request.Payload);

        var session = _sessions.FindByName(value.SessionName);

        var payload = session is null
            ? GameSessionContractCodec.EncodeFindSessionResponse(
                value.RequestHandlerSessionId,
                value.OriginClusterHash,
                value.PlayerId.Value,
                success: false,
                sessionId: 0,
                sessionName: value.SessionName,
                minimumPlayerCount: 0,
                maximumPlayerCount: 0)
            : GameSessionContractCodec.EncodeFindSessionResponse(
                value.RequestHandlerSessionId,
                value.OriginClusterHash,
                value.PlayerId.Value,
                success: true,
                sessionId: session.SessionId.Value,
                sessionName: session.Name,
                minimumPlayerCount: session.MinimumPlayers,
                maximumPlayerCount: session.MaximumPlayers);

        return new ServiceResponse(
            request.DataType,
            value.PlayerId.Value,
            payload,
            request.Compression);
    }

    private ServiceResponse HandleJoinSession(
        HcpServiceRequest request)
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
            false,
            session?.Players ??
                Array.Empty<(ulong PlayerId, int Position)>());

        return new ServiceResponse(
            request.DataType,
            value.PlayerId.Value,
            GameSessionContractCodec.EncodeJoinSessionResponse(response),
            request.Compression);
    }
}
