using System.Collections.Concurrent;
using System.Text.Json;
using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public sealed class HcpReliableChannel
{
    private readonly ConcurrentDictionary<long, HcpMessage> _sent = new();
    private long _ccnt;
    private long _scnt;

    public long ClientCounter => Interlocked.Read(ref _ccnt);
    public long ServerCounter => Interlocked.Read(ref _scnt);

    public long NextClientCounter() => Interlocked.Increment(ref _ccnt);
    public long NextServerCounter() => Interlocked.Increment(ref _scnt);

    public void TrackClientMessage(long ccnt, HcpMessage message) => _sent[ccnt] = message;

    public bool TryGetSent(long ccnt, out HcpMessage? message) => _sent.TryGetValue(ccnt, out message);

    public void AcknowledgeThrough(long ccnt)
    {
        foreach (var key in _sent.Keys)
        {
            if (key <= ccnt)
                _sent.TryRemove(key, out _);
        }
    }

    public bool IsNextServerMessage(long receivedScnt)
        => receivedScnt == ServerCounter + 1;

    public byte[] BuildResendRequest(long requestedCounter)
        => HcpHeaderCodec.Encode(new Dictionary<string, object?>
        {
            ["target"] = "rsnd",
            ["instance"] = "req",
            ["scnt"] = ServerCounter,
            ["ccnt"] = ClientCounter,
            ["req"] = requestedCounter
        });
}
