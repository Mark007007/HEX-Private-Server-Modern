using System.Collections.Concurrent;
using HexServer.Contracts;

namespace HexServer.Server;

public sealed class GameSessionRecord
{
    private readonly object _gate = new();
    private readonly List<(ulong PlayerId, int Position)> _players = new();

    public GameSessionRecord(
        HexUid sessionId,
        string name,
        int minimumPlayers,
        int maximumPlayers)
    {
        SessionId = sessionId;
        Name = name;
        MinimumPlayers = minimumPlayers;
        MaximumPlayers = maximumPlayers;
    }

    public HexUid SessionId { get; }
    public string Name { get; }
    public int MinimumPlayers { get; }
    public int MaximumPlayers { get; }

    public IReadOnlyList<(ulong PlayerId, int Position)> Players
    {
        get
        {
            lock (_gate)
                return _players.ToArray();
        }
    }

    public bool TryAddPlayer(ulong playerId, int position)
    {
        lock (_gate)
        {
            if (_players.Any(x => x.PlayerId == playerId))
                return true;

            if (_players.Count >= MaximumPlayers)
                return false;

            if (_players.Any(x => x.Position == position))
                return false;

            _players.Add((playerId, position));
            return true;
        }
    }
}

public sealed class GameSessionRegistry
{
    private readonly ConcurrentDictionary<ulong, GameSessionRecord> _sessions = new();
    private long _nextInstance = 1000;

    public GameSessionRecord Create(
        string name,
        int minimumPlayers,
        int maximumPlayers,
        ulong ownerPlayerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Session name is required.", nameof(name));

        if (minimumPlayers < 1 || maximumPlayers < minimumPlayers)
            throw new ArgumentOutOfRangeException(nameof(maximumPlayers));

        var instance = unchecked((ulong)Interlocked.Increment(ref _nextInstance));
        var id = new HexUid(UidCodec.Make(13, instance));

        var record = new GameSessionRecord(id, name, minimumPlayers, maximumPlayers);
        if (!record.TryAddPlayer(ownerPlayerId, 0))
            throw new InvalidOperationException("Could not add session owner.");

        if (!_sessions.TryAdd(id.Value, record))
            throw new InvalidOperationException("Could not register game session.");

        return record;
    }

    public bool TryGet(HexUid id, out GameSessionRecord? record)
        => _sessions.TryGetValue(id.Value, out record);

    public GameSessionRecord? FindByName(string name)
        => _sessions.Values.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.Ordinal));

    public bool TryAddPlayer(
        HexUid sessionId,
        ulong playerId,
        int position)
        => _sessions.TryGetValue(sessionId.Value, out var record) &&
           record.TryAddPlayer(playerId, position);
}

public static class UidCodec
{
    public static ulong Make(byte type8, ulong instanceId56)
        => type8 | ((instanceId56 & 0x00FFFFFFFFFFFFFFUL) << 8);
}
