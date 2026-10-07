using System.Net;
using System.Net.Sockets;
using HexServer.Core.Game;
using HexServer.Protocol.HConnect;

namespace HexServer.Server;

public sealed class HcpTcpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly SessionRegistry _sessions;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextConnectionId;

    public HcpTcpServer(IPEndPoint endpoint, SessionRegistry sessions)
    {
        _listener = new TcpListener(endpoint);
        _sessions = sessions;
    }

    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return Task.CompletedTask;
        IsRunning = true;
        _listener.Start();
        _ = AcceptLoopAsync(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken externalCancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token, externalCancellationToken);

        try
        {
            while (!linked.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(linked.Token).ConfigureAwait(false);
                _ = HandleClientAsync(client, linked.Token);
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (linked.IsCancellationRequested)
        {
        }
        finally
        {
            IsRunning = false;
            _stopped.TrySetResult();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var _ = client.ConfigureAwait(false);
        var connectionId = $"tcp-{Interlocked.Increment(ref _nextConnectionId)}";
        await using var connection = new HcpConnection(client.GetStream());
        var session = new HcpSession(connectionId);
        session.BeginHandshake();

        try
        {
            await foreach (var frame in connection.ReadFramesAsync(cancellationToken))
            {
                var header = HcpHeaderCodec.Decode(frame.Header);
                var message = new HcpMessage(header, frame.Body);

                if (message.TryGetString("target", out var target) &&
                    string.Equals(target, "newsession", StringComparison.Ordinal))
                {
                    var sid = _sessions.AllocateId();
                    if (!_sessions.TryAdd(sid, session))
                        throw new InvalidOperationException($"Could not register session {sid}.");

                    var response = session.BuildCreateResponse(sid);
                    await connection.WriteFrameAsync(response, cancellationToken);
                    continue;
                }

                if (message.TryGetString("target", out target) &&
                    string.Equals(target, "close", StringComparison.Ordinal))
                    break;

                // Protocol parsing is deliberately conservative here. Unknown messages
                // are not interpreted until their service contract is recovered.
            }
        }
        catch (InvalidDataException)
        {
            // Malformed HCP connection: fail closed.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (session.State.SessionId is ulong sid)
                _sessions.TryRemove(sid);
            session.State.Close();
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
