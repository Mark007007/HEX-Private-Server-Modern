
# HEX Private Server Modern — Reverse Engineering Record

本文只记录来自授权研究样本、客户端程序集元数据/IL、以及公开旧实现交叉验证后的结果。

## Evidence levels

- CONFIRMED — 直接来自 CLI metadata、IL、常量或已确认代码路径。
- HIGH CONFIDENCE — 多个独立路径一致，但尚未完成原客户端动态 capture。
- RECONSTRUCTED — 根据 IL + 行为恢复，必须继续用 golden bytes 验证。
- UNVERIFIED — 不进入正式协议常量。

## HConnect frame

CONFIRMED

    ~HCP~                 5 bytes
    contentSize           uint32 BE
    headerSize             uint32 BE
    header                 UTF-8 JSON
    bodySize               uint32 BE
    body                   binary

    contentSize = 8 + headerSize + bodySize

TCP 必须支持半包、粘包、多个 frame 一次读取。

## HConnect session reliability

客户端维护：

    CCnt
    SCnt
    _sessionId
    _Outgoing
    _Received
    _bufferedSent
    _resend
    _lastReq

观察到的 header 字段：

    ccnt
    scnt
    time
    sid
    version
    tags

重传路径使用 target=rsnd、instance=req。

## Session handshake

CONFIRMED

Client -> Server：

    target=newsession

Server -> Client：

    issuer=Session
    target=create
    sid=<session id>

当前客户端构建中存在 9933 fallback/default 证据，因此本项目默认开发监听 9933，但仍允许覆盖。

## DataWrapper

CLI metadata 已确认字段顺序：

    RequestId                   Int64
    DataType                    Int32
    Bytes                       Byte[]
    RequestHandlerSessionId    Guid
    Comp                        Byte

编码链：

    DataWrapper
      -> EncData
      -> custom ObjFmt

## ObjFmt

CONFIRMED / RECONSTRUCTED

已确认：

- separator = ;
- root object 使用 empty name + size index + type index + property count
- field 使用 name + size index + type index + property count
- type table
- size table
- UTF-8 string 带 byte length
- bool = 0 / 1
- numeric primitive 经 BitConverter.GetBytes 后进入 hex text
- enum 使用 value__ Int32
- UID 使用 m_UID64
- byte[] 使用 4-byte big-endian length + raw bytes
- nested user structs 递归编码

数字 hex 文本必须匹配客户端实际大小写行为；当前路径为小写 ASCII hex。

完整复合对象的 byte-for-byte fixture 仍是最终协议验收标准。

## Service IDs

    242 Tournaments
    243 Monitor
    245 Profile
    246 GameSession
    247 Matchmaking
    248 AI
    249 Escrow
    251 GM
    252 Mail
    253 Campaign
    254 LoadBalancer

## GameSession request IDs

    3003 TryReconnectionToDisconnectedGame
    3005 StartSession
    3007 StartEncounter
    3009 FindReconnectionInformation
    3011 FindSession
    3013 JoinDisconnectedGame
    3015 JoinSession
    3019 ReadyForGameSetup
    3025 LeaveSession
    3027 EndSession
    3031 GetSessionList
    3047 FindSessionById
    3049 SessionResync
    3050 PlayerAdded
    3051 PlayerRemoved
    3052 GameContinue
    3053 GameStarted
    3054 GameEnded
    3055 SessionSyncEvent
    3056 ChampionStatsUpdated

## GameSession request field recovery

### StartSessionRequestArgs

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionName             : String
    MinPlayers              : Int32
    MaxPlayers              : Int32
    AIPlayers               : Int32
    ParticipatingPlayers    : List<Game.Shared.RemotePlayer>
    SessionFlags            : Int32
    Observable              : Boolean
    FilterID                : Game.Shared.ResourceId
    TestDeckId              : UInt64

### FindSessionRequestArgs

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionName             : String

### JoinSessionRequestArgs

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionId               : Game.Shared.UID
    DeckID                  : UInt64
    DeckTemplateID           : Game.Shared.ResourceId
    PlayerPosition           : Int32
    ChampionID               : UInt64
    SelfTurnPhases           : List<ETurnPhases>
    OpponentTurnPhases       : List<ETurnPhases>

### ReadyForGameSetupRequestArgs

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionId               : Game.Shared.UID
    SelfTurnPhases           : List<ETurnPhases>
    OpponentTurnPhases       : List<ETurnPhases>

### ReadyForGameEventsRequestArgs

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionId               : Game.Shared.UID

### ReadyToStartGameRequestArgs

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    IsReady                  : Boolean

### PlayerTransactionRequestArgs

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    Transaction              : Transaction

## GameSession response recovery

StartSessionResponseArgs:

    RequestHandlerSessionId
    OriginClusterHash
    RoutingPlayerId
    Success
    SessionState

FindSessionResponseArgs:

    RequestHandlerSessionId
    OriginClusterHash
    RoutingPlayerId
    Success
    SessionState

JoinSessionResponseArgs:

    RequestHandlerSessionId
    OriginClusterHash
    RoutingPlayerId
    Success
    SessionState
    SessionPlayers

ReadyForGameSetupResponseArgs:

    RequestHandlerSessionId
    OriginClusterHash
    SessionState
    DeckId
    DeckTemplateId
    OpponentsInfo
    TurnOrder
    seedZ
    seedW

ReadyForGameEventsResponseArgs, ReadyToStartGameResponseArgs and PlayerTransactionResponseArgs are enums:

    Ok                  = 0
    InternalServerError = -1
    RequestTimeoutError = -2

## Shared rule-core reuse

Highest priority reusable regions:

    Game.Shared.Mechanics
    Game.Shared.Session
    Game.Shared.AuthoritativeSessionBase
    Game.Shared.Mechanics.Transactions
    SessionEventArgs / Session event types
    Ability / Effect / Combat / Requirement / Modifier

The examined Mechanics/Session regions have no direct UnityEngine calls in the static dependency scan.

AuthoritativeSessionBase has a small number of Game.Client.Network input dependencies around profile/deck and encounter modifications. These should be replaced by server adapters rather than rewriting the whole rules core.

## Transaction model

Transaction already exposes:

    Initialize
    Require
    Validate
    Resolve
    GetSerializedBytes

The server should reuse this model whenever the original runtime can be loaded or the corresponding behavior can be ported safely.

## Session event model

The client contains rich events for:

- game start/end
- phase and priority
- draw/move/destroy/discard
- card play
- attack/block/combat
- resources
- champion state
- ability chain
- card updates
- player updates
- private option lists

NetworkPacketSessionEventArgs is the 3055 synchronization envelope.

Event order and visibility are part of compatibility.

## Compatibility boundary

    Original Shared Core
          |
          v
    HexLegacyCompat / adapters
          |
          v
    HexServer.Game
          |
          v
    HexServer.Protocol
          |
          v
    HConnect

The final server should not depend on Unity rendering, scene objects, MonoBehaviour or client UI.

## Explicitly not assumed

No current evidence proves:

- RabbitMQ is the player-facing protocol.
- SmartFox is the exact HConnect implementation.
- HCP packet encryption is AES/RSA/DH.
- the old 0x4858 six-byte packet format was real HEX protocol.

## Next gates

1. Exact DataWrapper golden equality.
2. Complete ObjFmt decoder.
3. Real ServiceGameSession request/response capture.
4. Recover 3055 SessionSyncEvent wrapper.
5. Recover Transaction object field layout.
6. Load/port the minimum Game.Shared dependency island.
7. First two-client local session.
