using System.Net;
using System.Net.Sockets;
using HexServer.Protocol.HConnect;
using HexServer.Protocol.Services;

namespace HexServer.Server;

public sealed class HcpTcpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly SessionRegistry _connections;
    private readonly ServiceRouter _router;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextConnectionId;

    public HcpTcpServer(
        IPEndPoint endpoint,
        SessionRegistry connections,
        ServiceRouter router)
    {
        _listener = new TcpListener(endpoint);
        _connections = connections;
        _router = router;
    }

    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return Task.CompletedTask;

        IsRunning = true;
        _listener.Start();
        _ = AcceptLoopAsync(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(
        CancellationToken externalCancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token,
            externalCancellationToken);

        try
        {
            while (!linked.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(linked.Token)
                    .ConfigureAwait(false);

                _ = HandleClientAsync(client, linked.Token);
            }
        }
        catch (OperationCanceledException)
            when (linked.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
            when (linked.IsCancellationRequested)
        {
        }
        finally
        {
            IsRunning = false;
            _stopped.TrySetResult();
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        await using (var connection =
            new HcpConnection(client.GetStream()))
        {
            var connectionId =
                $"tcp-{Interlocked.Increment(ref _nextConnectionId)}";

            var session = new HcpSession(connectionId);
            session.BeginHandshake();

            try
            {
                await foreach (var frame in connection.ReadFramesAsync(
                    cancellationToken))
                {
                    var header = HcpHeaderCodec.Decode(frame.Header);
                    var message = new HcpMessage(header, frame.Body);

                    if (message.TryGetString("target", out var target))
                    {
                        if (string.Equals(
                            target,
                            "newsession",
                            StringComparison.Ordinal))
                        {
                            var sid = _connections.AllocateId();

                            if (!_connections.TryAdd(sid, session))
                                throw new InvalidOperationException(
                                    $"Could not register HCP session {sid}.");

                            var createResponse =
                                session.BuildCreateResponse(sid);

                            await connection.WriteFrameAsync(
                                createResponse,
                                cancellationToken);

                            continue;
                        }

                        if (string.Equals(
                            target,
                            "close",
                            StringComparison.Ordinal))
                        {
                            break;
                        }
                    }

                    if (!HcpServiceMessage.TryDecode(
                        message,
                        out var serviceRequest))
                    {
                        // Unknown or not-yet-reconstructed messages are not
                        // guessed. This keeps the wire implementation fail-closed.
                        continue;
                    }

                    session.Reliability.ObserveClientCounter(
                        serviceRequest.ClientCounter);

                    var serviceResponse =
                        await _router.DispatchAsync(
                            serviceRequest,
                            cancellationToken)
                        .ConfigureAwait(false);

                    var sessionId =
                        serviceRequest.SessionId ??
                        session.State.SessionId ??
                        throw new InvalidOperationException(
                            "Service request has no active HCP session.");

                    var clientUid =
                        serviceResponse.RoutingPlayerId ??
                        0UL;

                    var response =
                        HcpResponseFactory.CreateServiceResponse(
                            serviceTarget: serviceRequest.Target,
                            serviceUid: serviceRequest.ServiceId,
                            clientUid: clientUid,
                            instance: serviceRequest.Instance,
                            requestId: serviceRequest.RequestId,
                            dataType: serviceResponse.DataType,
                            compression: serviceResponse.Compression,
                            connectionHandle: serviceRequest.ConnectionHandle,
                            sessionId: sessionId,
                            serverCounter: session.Reliability.NextServerCounter(),
                            clientCounter: session.Reliability.ClientCounter,
                            requestHandlerSessionId:
                                serviceRequest.RequestHandlerSessionId,
                            uncompressedResponsePayload:
                                serviceResponse.Payload);

                    await connection.WriteFrameAsync(
                        response,
                        cancellationToken);
                }
            }
            catch (InvalidDataException)
            {
                // Fail closed on malformed protocol input.
            }
            catch (KeyNotFoundException)
            {
                // Unknown service/method is intentionally not guessed.
            }
            catch (NotSupportedException)
            {
                // A valid but not-yet-implemented service does not poison the socket.
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                if (session.State.SessionId is ulong sid)
                    _connections.TryRemove(sid);

                session.State.Close();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();

        await _stopped.Task.ConfigureAwait(false);
        _cts.Dispose();
    }
}
