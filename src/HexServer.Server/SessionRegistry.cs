using System.Collections.Concurrent;
using HexServer.Protocol.HConnect;

namespace HexServer.Server;

public sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<ulong, HcpSession> _sessions = new();
    private long _nextId = 10000;

    public ulong AllocateId()
        => unchecked((ulong)Interlocked.Increment(ref _nextId));

    public bool TryAdd(ulong id, HcpSession session)
        => _sessions.TryAdd(id, session);

    public bool TryGet(ulong id, out HcpSession? session)
        => _sessions.TryGetValue(id, out session);

    public bool TryRemove(ulong id)
        => _sessions.TryRemove(id, out _);

    public int Count => _sessions.Count;
}
