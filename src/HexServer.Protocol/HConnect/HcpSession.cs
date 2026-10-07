using HexServer.Core.Game;
using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public sealed class HcpSession
{
    private readonly HcpReliableChannel _reliability = new();

    public HcpSession(string connectionId)
    {
        ConnectionId = connectionId;
        State = new SessionState();
    }

    public string ConnectionId { get; }
    public SessionState State { get; }
    public HcpReliableChannel Reliability => _reliability;

    public HcpFrame BuildCreateResponse(ulong sessionId, string version = "1")
    {
        State.Establish(sessionId);
        return HcpMessageFactory.CreateSession(
            sessionId,
            _reliability.NextServerCounter(),
            _reliability.ClientCounter,
            version);
    }

    public void BeginHandshake() => State.BeginHandshake();
}
