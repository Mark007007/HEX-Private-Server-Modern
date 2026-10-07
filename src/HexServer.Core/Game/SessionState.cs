namespace HexServer.Core.Game;

public enum SessionConnectionState
{
    New,
    Handshaking,
    Connected,
    Closing,
    Closed
}

public sealed class SessionState
{
    public SessionConnectionState Connection { get; private set; } = SessionConnectionState.New;
    public ulong? SessionId { get; private set; }

    public void BeginHandshake() => Connection = SessionConnectionState.Handshaking;

    public void Establish(ulong sessionId)
    {
        SessionId = sessionId;
        Connection = SessionConnectionState.Connected;
    }

    public void Close() => Connection = SessionConnectionState.Closed;
}
