using System.Collections.Concurrent;
using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public sealed class HcpReliableChannel
{
    private readonly ConcurrentDictionary<long, HcpMessage> _serverSent = new();
    private long _clientCounter;
    private long _serverCounter;

    public long ClientCounter => Interlocked.Read(ref _clientCounter);
    public long ServerCounter => Interlocked.Read(ref _serverCounter);

    public long NextServerCounter()
        => Interlocked.Increment(ref _serverCounter);

    public void ObserveClientCounter(long value)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _clientCounter);

            if (value <= current)
                return;

            if (Interlocked.CompareExchange(
                    ref _clientCounter,
                    value,
                    current) == current)
            {
                return;
            }
        }
    }

    public bool ObserveServerAcknowledgement(long value)
    {
        if (value < 0)
            return false;

        AcknowledgeServerThrough(value);
        return true;
    }

    public void TrackServerMessage(
        long scnt,
        HcpMessage message)
    {
        if (scnt <= 0)
            return;

        _serverSent[scnt] = message;
    }

    public bool TryGetServerMessage(
        long scnt,
        out HcpMessage? message)
        => _serverSent.TryGetValue(scnt, out message);

    public void AcknowledgeServerThrough(long scnt)
    {
        foreach (var key in _serverSent.Keys)
        {
            if (key <= scnt)
                _serverSent.TryRemove(key, out _);
        }
    }

    public bool IsExpectedServerCounter(long receivedScnt)
        => receivedScnt == ServerCounter + 1;

    public HcpFrame BuildResendRequest(long requestedCounter)
    {
        var header = HcpHeaderCodec.Encode(new Dictionary<string, object?>
        {
            ["target"] = "rsnd",
            ["instance"] = "req",
            ["scnt"] = ServerCounter,
            ["ccnt"] = ClientCounter,
            ["req"] = requestedCounter
        });

        return new HcpFrame(header, Array.Empty<byte>());
    }
}
