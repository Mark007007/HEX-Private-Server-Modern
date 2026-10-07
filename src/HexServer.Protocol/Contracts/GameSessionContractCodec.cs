using HexServer.Contracts;
using HexServer.Protocol.ObjFmt;

namespace HexServer.Protocol.Contracts;

public static class GameSessionContractCodec
{
    public static StartSessionRequest DecodeStartSession(ReadOnlySpan<byte> bytes)
    {
        var doc = ObjFmtDocument.Parse(bytes);

        return new StartSessionRequest(
            ReadGuid(doc, "RequestHandlerSessionId"),
            ReadInt(doc, "OriginClusterHash"),
            new HexUid(ReadUid(doc, "PlayerId")),
            ReadString(doc, "SessionName"),
            ReadInt(doc, "MinPlayers"),
            ReadInt(doc, "MaxPlayers"),
            ReadInt(doc, "AIPlayers"),
            ReadRemotePlayers(doc, "ParticipatingPlayers"),
            ReadInt(doc, "SessionFlags"),
            ReadBool(doc, "Observable"),
            new HexResourceId(ReadResourceGuid(doc, "FilterID")),
            ReadUInt64(doc, "TestDeckId"));
    }

    public static FindSessionRequest DecodeFindSession(ReadOnlySpan<byte> bytes)
    {
        var doc = ObjFmtDocument.Parse(bytes);

        return new FindSessionRequest(
            ReadGuid(doc, "RequestHandlerSessionId"),
            ReadInt(doc, "OriginClusterHash"),
            new HexUid(ReadUid(doc, "PlayerId")),
            ReadString(doc, "SessionName"));
    }

    public static JoinSessionRequest DecodeJoinSession(ReadOnlySpan<byte> bytes)
    {
        var doc = ObjFmtDocument.Parse(bytes);

        return new JoinSessionRequest(
            ReadGuid(doc, "RequestHandlerSessionId"),
            ReadInt(doc, "OriginClusterHash"),
            new HexUid(ReadUid(doc, "PlayerId")),
            new HexUid(ReadUid(doc, "SessionId")),
            ReadUInt64(doc, "DeckID"),
            new HexResourceId(ReadResourceGuid(doc, "DeckTemplateID")),
            ReadInt(doc, "PlayerPosition"),
            ReadUInt64(doc, "ChampionID"),
            ReadEnumList(doc, "SelfTurnPhases"),
            ReadEnumList(doc, "OpponentTurnPhases"));
    }

    public static ReadyForGameSetupRequest DecodeReadyForGameSetup(ReadOnlySpan<byte> bytes)
    {
        var doc = ObjFmtDocument.Parse(bytes);

        return new ReadyForGameSetupRequest(
            ReadGuid(doc, "RequestHandlerSessionId"),
            ReadInt(doc, "OriginClusterHash"),
            new HexUid(ReadUid(doc, "PlayerId")),
            new HexUid(ReadUid(doc, "SessionId")),
            ReadEnumList(doc, "SelfTurnPhases"),
            ReadEnumList(doc, "OpponentTurnPhases"));
    }

    public static ReadyForGameEventsRequest DecodeReadyForGameEvents(ReadOnlySpan<byte> bytes)
    {
        var doc = ObjFmtDocument.Parse(bytes);

        return new ReadyForGameEventsRequest(
            ReadGuid(doc, "RequestHandlerSessionId"),
            ReadInt(doc, "OriginClusterHash"),
            new HexUid(ReadUid(doc, "PlayerId")),
            new HexUid(ReadUid(doc, "SessionId")));
    }

    public static ReadyToStartGameRequest DecodeReadyToStartGame(ReadOnlySpan<byte> bytes)
    {
        var doc = ObjFmtDocument.Parse(bytes);

        return new ReadyToStartGameRequest(
            ReadGuid(doc, "RequestHandlerSessionId"),
            ReadInt(doc, "OriginClusterHash"),
            new HexUid(ReadUid(doc, "PlayerId")),
            ReadBool(doc, "IsReady"));
    }

    public static PlayerTransactionRequest DecodePlayerTransaction(ReadOnlySpan<byte> bytes)
    {
        var doc = ObjFmtDocument.Parse(bytes);
        var transaction = Require(doc, "Transaction");

        return new PlayerTransactionRequest(
            ReadGuid(doc, "RequestHandlerSessionId"),
            ReadInt(doc, "OriginClusterHash"),
            new HexUid(ReadUid(doc, "PlayerId")),
            transaction.Payload);
    }

    public static byte[] EncodeStartSessionResponse(StartSessionResponse value)
    {
        var b = new ObjFmtBuilder("Game.Shared.Network.GameSession.StartSessionResponseArgs");
        b.FieldGuid("RequestHandlerSessionId", value.RequestHandlerSessionId);
        b.FieldInt("OriginClusterHash", value.OriginClusterHash);
        b.FieldUid("RoutingPlayerId", value.RoutingPlayerId.Value);
        b.FieldBool("Success", value.Success);
        b.FieldSessionState(
            "SessionState",
            value.SessionId.Value,
            value.SessionName,
            value.MinimumPlayerCount,
            value.MaximumPlayerCount,
            sceneTemplateId: Guid.Empty,
            sessionFlags: 0,
            sessionUid: value.SessionId.Value,
            joinInsteadOfReconnect: value.JoinInsteadOfReconnect);
        return b.Finish(5);
    }

    public static byte[] EncodeFindSessionResponse(
        Guid requestHandlerSessionId,
        int originClusterHash,
        ulong routingPlayerId,
        bool success,
        ulong sessionId,
        string sessionName,
        int minimumPlayerCount,
        int maximumPlayerCount,
        bool joinInsteadOfReconnect = false)
    {
        var b = new ObjFmtBuilder("Game.Shared.Network.GameSession.FindSessionResponseArgs");
        b.FieldGuid("RequestHandlerSessionId", requestHandlerSessionId);
        b.FieldInt("OriginClusterHash", originClusterHash);
        b.FieldUid("RoutingPlayerId", routingPlayerId);
        b.FieldBool("Success", success);
        b.FieldSessionState(
            "SessionState",
            sessionId,
            sessionName,
            minimumPlayerCount,
            maximumPlayerCount,
            sceneTemplateId: Guid.Empty,
            sessionFlags: 0,
            sessionUid: sessionId,
            joinInsteadOfReconnect: joinInsteadOfReconnect);
        return b.Finish(5);
    }

    public static byte[] EncodeJoinSessionResponse(JoinSessionResponse value)
    {
        var b = new ObjFmtBuilder("Game.Shared.Network.GameSession.JoinSessionResponseArgs");
        b.FieldGuid("RequestHandlerSessionId", value.RequestHandlerSessionId);
        b.FieldInt("OriginClusterHash", value.OriginClusterHash);
        b.FieldUid("RoutingPlayerId", value.RoutingPlayerId.Value);
        b.FieldBool("Success", value.Success);
        b.FieldSessionState(
            "SessionState",
            value.SessionId.Value,
            value.SessionName,
            value.MinimumPlayerCount,
            value.MaximumPlayerCount,
            value.JoinInsteadOfReconnect);
        b.FieldPlayerStateList("SessionPlayers", value.SessionPlayers);
        return b.Finish(6);
    }

    private static ObjFmtField Require(ObjFmtDocument doc, string name)
        => doc.Fields.FirstOrDefault(x => x.Name == name)
           ?? throw new InvalidDataException(
               $"Required ObjFmt field missing: {name}");

    private static int ReadInt(ObjFmtDocument doc, string name)
        => HexFieldReader.ReadInt32(Require(doc, name));

    private static ulong ReadUInt64(ObjFmtDocument doc, string name)
        => HexFieldReader.ReadUInt64(Require(doc, name));

    private static ulong ReadUid(ObjFmtDocument doc, string name)
        => HexFieldReader.ReadUid64(Require(doc, name), doc.Sizes);

    private static Guid ReadGuid(ObjFmtDocument doc, string name)
        => HexFieldReader.ReadGuid(Require(doc, name));

    private static Guid ReadResourceGuid(ObjFmtDocument doc, string name)
        => HexFieldReader.ReadResourceGuid(Require(doc, name), doc.Sizes);

    private static string ReadString(ObjFmtDocument doc, string name)
        => HexFieldReader.ReadString(Require(doc, name));

    private static bool ReadBool(ObjFmtDocument doc, string name)
        => HexFieldReader.ReadBool(Require(doc, name));

    private static IReadOnlyList<HexRemotePlayer> ReadRemotePlayers(
        ObjFmtDocument doc,
        string name)
    {
        var field = Require(doc, name);
        var result = new List<HexRemotePlayer>();

        foreach (var item in ObjFmtDocument.ReadListElements(field, doc.Sizes))
        {
            var element = new ObjFmtField(
                "element",
                item.SizeIndex,
                item.TypeIndex,
                item.PropertyCount,
                item.Payload);

            foreach (var nested in ObjFmtDocument.ReadNestedFields(element, doc.Sizes))
            {
                if (nested.Name is "m_Guid" or "Guid")
                    result.Add(new HexRemotePlayer(HexFieldReader.ReadGuid(nested)));
            }
        }

        return result;
    }

    private static IReadOnlyList<int> ReadEnumList(
        ObjFmtDocument doc,
        string name)
    {
        var field = Require(doc, name);
        var values = new List<int>();

        foreach (var item in ObjFmtDocument.ReadListElements(field, doc.Sizes))
        {
            var element = new ObjFmtField(
                "element",
                item.SizeIndex,
                item.TypeIndex,
                item.PropertyCount,
                item.Payload);

            values.Add(HexFieldReader.ReadEnumValue(element, doc.Sizes));
        }

        return values;
    }
}
