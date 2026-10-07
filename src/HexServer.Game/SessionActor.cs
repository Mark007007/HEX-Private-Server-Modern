using System.Threading.Channels;
using HexServer.Core.Game;

namespace HexServer.Game;

public sealed class SessionActor : IAsyncDisposable
{
    private readonly SessionMailbox<SessionCommand> _mailbox;
    private readonly SessionState _state = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;

    public SessionActor(int mailboxCapacity = 256)
    {
        _mailbox = new SessionMailbox<SessionCommand>(mailboxCapacity);
    }

    public SessionState State => _state;

    public void Start() => _runTask ??= RunAsync(_cts.Token);

    public ValueTask PostAsync(SessionCommand command, CancellationToken cancellationToken = default)
        => _mailbox.EnqueueAsync(command, cancellationToken);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var command in _mailbox.ReadAllAsync(cancellationToken))
        {
            switch (command)
            {
                case HandshakeCommand handshake:
                    HandleHandshake(handshake);
                    break;
                case ServiceRequestCommand:
                    EnsureConnected();
                    break;
            }
        }
    }

    private void HandleHandshake(HandshakeCommand command)
    {
        if (!string.Equals(command.Target, "newsession", StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected session handshake target: {command.Target}");

        _state.BeginHandshake();
    }

    private void EnsureConnected()
    {
        if (_state.Connection != SessionConnectionState.Connected)
            throw new InvalidOperationException("Session is not connected.");
    }

    public async ValueTask DisposeAsync()
    {
        _mailbox.Complete();
        _cts.Cancel();
        if (_runTask is not null)
        {
            try { await _runTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _cts.Dispose();
    }
}
