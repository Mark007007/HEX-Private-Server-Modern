# HEX Private Server Modern

现代化、低耦合的 HEX: Shards of Fate 本地/私服兼容服务器。

> 核心原则：**不是重写 HEX，而是尽可能让原 HEX 客户端继续做原来的事情；新服务器只补回已经丢失的服务器职责。**

---

## 1. 项目最终方向

原客户端已经保存了大量比任何“重新设计”更有价值的信息：

- Game.Shared 游戏数据结构
- Mechanics / Card / Ability / Effect
- Session / AuthoritativeSessionBase
- Transaction
- SessionEventArgs
- HConnect 网络层
- DataWrapper / EncData / ObjFmt
- Service / Request / Response contracts
- LoadBalancer / GameSession / Profile 等服务契约

因此本项目采用：

    原 HEX Unity 客户端
             |
             v
    HConnect / HCP
             |
             v
    Modern HEX Server
       |       |       |
       |       |       +-- Persistence
       |       +---------- Session / Transaction
       +------------------ Protocol / Service
             |
             v
       Game.Shared 兼容层
             |
             v
       原版规则语义

服务器基础设施使用现代 C# / .NET 10。

原客户端 DLL 默认只作为：

1. 协议与数据结构 Oracle
2. 行为参考
3. 必要时的最小兼容依赖

不会把整个 Unity 客户端直接变成服务器运行时。

---

# 2. 为什么不继续使用旧 HEX-Private-Server

旧项目曾经同时加入：

- Dingler/FrostRingArena
- 原版 AI Worker
- Python AI
- Deck Import
- 多套 overlay
- 多套 server implementation
- 大量测试与脚本

结果是依赖关系越来越复杂，出现“为了修 A 必须启动 B/C/D”的问题。

Modern 项目明确取消这个方向。

### Modern 的原则

    Protocol first
    Session second
    Transaction third
    Rules fourth
    Optional services last

第一目标不是把所有功能一次性搬回来。

第一目标是：

> 让原客户端能够连接、建立 Session、收到原格式 Response，并最终进入第一场本地双客户端对战。

---

# 3. 当前协议链

目前 main 已经形成：

    Original HEX Client
          |
          v
    HConnect TCP
          |
          v
    HCP frame
          |
          v
    HConnect header
          |
          v
    DataWrapper
          |
          v
    ObjFmt
          |
          v
    Service
          |
          v
    Request contract
          |
          v
    Session / Service handler
          |
          v
    Response contract
          |
          v
    ObjFmt
          |
          v
    DataWrapper
          |
          v
    HCP response

当前默认本地监听：

    127.0.0.1:9933

这个端口来自客户端现有配置/fallback 证据；在没有真实线上 capture 前，不把它描述为历史线上服务器的绝对事实。

---

# 4. 已确认的 HCP 外层结构

当前逆向结果确认 HCP 使用：

    ~HCP~

随后是：

    contentSize   uint32 Big Endian
    headerSize    uint32 Big Endian
    header        UTF-8 JSON
    bodySize      uint32 Big Endian
    body          binary

并满足：

    contentSize = 8 + headerSize + bodySize

TCP 实现必须处理：

- 半包
- 粘包
- 单次 read 包含多个 frame
- connection close
- malformed length
- oversized frame

因此服务器绝不能假设“一次 Receive 就是一整个 HEX packet”。

---

# 5. HCP Session reliability

客户端现有代码中可以观察到：

    CCnt
    SCnt
    sid
    time
    version
    tags
    resend state
    buffered sent
    last request

Modern Server 已建立对应的可靠通道状态：

    ClientCounter
    ServerCounter
    server resend cache

其中：

- CCnt：客户端发送方向计数
- SCnt：服务器发送方向计数
- 服务器重传缓存按 SCnt 保存
- 客户端最新 CCnt 可以在 response header 中回显
- rsnd 请求用于请求缺失的服务器消息

这一层必须在 Service/Game Logic 之前完成，否则后面的 SessionSyncEvent 会出现难以诊断的乱序问题。

---

# 6. DataWrapper

客户端元数据已经确认 DataWrapper 的核心字段：

    RequestId
    DataType
    Bytes
    RequestHandlerSessionId
    Comp

当前实现已经建立对应的 binary wrapper。

重要：

> DataWrapper 不是 JSON RPC。

JSON 只出现在 HCP 外层 header；真正的 Service Request / Response 对象进入 ObjFmt binary payload。

---

# 7. ObjFmt

ObjFmt 是当前最重要的逆向工作之一。

已经确认/重建的能力包括：

- primitive
- string
- bool
- numeric
- enum / value__
- UID
- ResourceId
- byte[]
- list
- nested object
- type table
- size table
- property count
- field name / type index / size index

当前仍需要继续覆盖：

- dictionary
- generic collection
- nullable / optional value
- 更复杂的 struct
- 所有特殊 serializer
- 真实客户端 golden bytes 的逐字节比对

因此：

> ObjFmt 当前不能被宣称“100% 完成”。

所有最终协议实现都必须通过 golden byte fixture 验证。

---

# 8. Service 边界

这是之前实现中出现过的重要错误，已经修正。

## LoadBalancer

    Service = 254

核心 GameSession 创建/寻找入口：

    22011 StartSession
    22013 StartEncounter
    22015 FindSession
    22017 JoinSession
    22019 ReadyForGameSetup
    22021 ReadyForGameEvents
    22023 JoinDisconnectedGame
    22025 ReadyToContinueGame
    22027 LeaveSession
    22029 EndSession
    22031 GetSessionList

## GameSession

    Service = 246

重要游戏事件/同步入口：

    3029 PlayerTransaction
    3050 PlayerAdded
    3051 PlayerRemoved
    3052 GameContinue
    3053 GameStarted
    3054 GameEnded
    3055 SessionSyncEvent
    3056 ChampionStatsUpdated

这个边界非常重要：

    LoadBalancer
        =
    “找到/创建/加入游戏 Session”

    GameSession
        =
    “Session 建立后的游戏运行时事件”

不要再次把两者合并。

---

# 9. 已恢复的 Request contracts

## StartSession

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

## FindSession

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionName             : String

## JoinSession

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

## ReadyForGameSetup

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionId               : Game.Shared.UID
    SelfTurnPhases           : List<ETurnPhases>
    OpponentTurnPhases       : List<ETurnPhases>

## ReadyForGameEvents

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    SessionId               : Game.Shared.UID

## PlayerTransaction

    RequestHandlerSessionId : Guid
    OriginClusterHash       : Int32
    PlayerId                : Game.Shared.UID
    Transaction              : Transaction

---

# 10. Response contracts

已恢复的重要 Response：

## StartSessionResponse

    RequestHandlerSessionId
    OriginClusterHash
    RoutingPlayerId
    Success
    SessionState

## FindSessionResponse

    RequestHandlerSessionId
    OriginClusterHash
    RoutingPlayerId
    Success
    SessionState

## JoinSessionResponse

    RequestHandlerSessionId
    OriginClusterHash
    RoutingPlayerId
    Success
    SessionState
    SessionPlayers

## ReadyForGameSetupResponse

    RequestHandlerSessionId
    OriginClusterHash
    SessionState
    DeckId
    DeckTemplateId
    OpponentsInfo
    TurnOrder
    seedZ
    seedW

Response error enum 当前确认：

    Ok                  = 0
    InternalServerError = -1
    RequestTimeoutError = -2

---

# 11. Session 设计

Modern Server 不再让 Socket handler 直接修改游戏状态。

采用：

    TCP connection
          |
          v
    Protocol decoder
          |
          v
    Service dispatcher
          |
          v
    Session mailbox
          |
          v
    ordered command
          |
          v
    state mutation
          |
          v
    events / response

同一个 Session 的操作必须保持顺序。

例如：

    Join
      ->
    ReadyForGameSetup
      ->
    ReadyForGameEvents
      ->
    ReadyToStartGame
      ->
    PlayerTransaction
      ->
    SessionSyncEvent

这样可以避免旧式“网络线程 + 游戏线程 + 回调线程”互相修改状态造成的死锁和竞态。

---

# 12. RabbitMQ 的正确定位

RabbitMQ 可以保留，但不能再成为每一个玩家操作的强制路径。

不推荐：

    Client
      ->
    Gateway
      ->
    RabbitMQ
      ->
    Logic
      ->
    RabbitMQ
      ->
    Gateway
      ->
    Client

因为这会：

- 增加延迟
- 增加故障面
- 让 Session 顺序更加复杂
- 让单机部署变得麻烦
- 无法解决真正的协议逆向问题

Modern 的默认路径：

    Client
      ->
    HConnect
      ->
    Session Actor
      ->
    Game Logic
      ->
    Response

RabbitMQ 只作为以后真正需要的：

- AI worker
- background jobs
- analytics
- asynchronous persistence
- multi-process service
- external tools

扩展。

---

# 13. 最大化利用原客户端的方法

这是本项目和普通“私服重写”最大的区别。

## 第一层：协议复用

直接恢复：

    HConnect
    HCP
    DataWrapper
    ObjFmt
    Service IDs
    Method IDs
    RequestArgs
    ResponseArgs

## 第二层：数据模型复用

尽可能保持：

    UID
    ResourceId
    Card
    Deck
    Champion
    SessionState
    EncounterData
    Player state

字段名称、顺序、类型都优先遵循原客户端。

## 第三层：规则复用

优先研究：

    Game.Shared.Mechanics
    Game.Shared.Session
    Game.Shared.AuthoritativeSessionBase
    Game.Shared.Mechanics.Transactions

而不是重新写一个“类似 HEX”的规则系统。

## 第四层：行为 Oracle

如果某个旧类型无法直接加载：

    Original DLL
          |
          v
    Offline Probe
          |
          v
    input
          ->
    original behaviour
          ->
    expected state/event

Modern Server 再实现同样行为。

这比凭感觉重写规则可靠得多。

---

# 14. Game.Shared 最小依赖岛

目标不是把所有旧 DLL 放进服务器。

目标是建立：

    Game.Shared
       |
       +-- Mechanics
       +-- Session
       +-- Transactions
       +-- Events
       +-- Data contracts
             |
             v
        Compatibility Adapter

然后把以下内容隔离掉：

    UnityEngine
    UI
    rendering
    input
    MonoBehaviour
    client menus
    client-only networking
    platform SDK

当前已有 LegacyLoadProbe，用于确认原 DLL 中的目标类型是否能够在隔离加载上下文中解析。

注意：

> 能成功反射加载，不等于已经证明可以在服务器上正常执行。

下一步必须进行实际方法调用和依赖图裁剪。

---

# 15. 逆向证据等级

整个仓库统一使用四级：

### CONFIRMED

直接来自：

- CLI metadata
- IL
- 常量
- 明确代码路径

### HIGH CONFIDENCE

多个独立证据一致，但还没有完整 runtime capture。

### RECONSTRUCTED

根据 IL/行为恢复，仍需要 golden bytes 或客户端动态行为验证。

### UNVERIFIED

目前只是假设。

UNVERIFIED 内容：

- 不进入正式协议常量
- 不作为架构事实写入代码
- 不应该因为“看起来合理”而继续传播

---

# 16. 明确禁止的假设

目前没有足够证据证明：

- RabbitMQ 是玩家核心协议
- SmartFox 是 HConnect 的确切实现
- HEX 使用 AES / RSA / DH 作为当前 HConnect payload 加密
- 0x4858 六字节包头是真实 HEX 玩家协议
- 所有旧版 Server 都能直接作为 Modern Server 运行

因此这些不会被硬编码成“原版协议”。

---

# 17. 当前工具

## HexExtractor

位置：

    tools/HexExtractor

作用：

读取原始 .NET DLL 的：

- Type
- Field
- Method
- Assembly Reference

输出机器可读 catalog。

## LegacyLoadProbe

位置：

    tools/LegacyLoadProbe

作用：

在隔离 AssemblyLoadContext 中尝试加载：

    Game.Shared.Mechanics.Card
    Game.Shared.Session
    Game.Shared.AuthoritativeSessionBase
    Game.Shared.Mechanics.Transactions.Transaction
    Game.Shared.Network.DataWrapper
    Game.Shared.Network.HConnect.Session

用于判断最小兼容依赖岛。

---

# 18. 逆向工作流

推荐顺序：

    ① Metadata
         |
         v
    ② IL
         |
         v
    ③ Protocol fixture
         |
         v
    ④ Runtime capture
         |
         v
    ⑤ Server implementation
         |
         v
    ⑥ Original client test
         |
         v
    ⑦ Differential test

而不是：

    猜
     ->
    写
     ->
    客户端报错
     ->
    到处打补丁

---

# 19. 当前真正的完成标准

不是“代码很多”。

而是下面这条链闭合：

    Client connect
        |
        v
    HConnect session
        |
        v
    auth
        |
        v
    StartSession
        |
        v
    FindSession
        |
        v
    JoinSession
        |
        v
    ReadyForGameSetup
        |
        v
    ReadyForGameEvents
        |
        v
    ReadyToStartGame
        |
        v
    GameStarted
        |
        v
    PlayerTransaction
        |
        v
    SessionSyncEvent
        |
        v
    GameEnded

最终必须能够让：

    Original Client A
           +
    Original Client B

进入：

    Local Session
        ->
    Real Game State
        ->
    Real Transactions
        ->
    Real Events

这才算 Modern Server 的第一阶段完成。

---

# 20. 下一阶段优先级

### P0 — Protocol correctness

- ObjFmt decoder 完整化
- DataWrapper golden bytes
- HCP reliability
- auth:req
- request/response exact fixture

### P1 — Session correctness

- ReadyForGameSetup
- ReadyForGameEvents
- ReadyToStartGame
- PlayerAdded / PlayerRemoved
- GameStarted / GameEnded
- 3055 SessionSyncEvent

### P2 — Transaction correctness

- Transaction base
- concrete transaction type registry
- AssignDamageOrder
- CommitTroopsToAttack
- CommitTroopsToDefense
- target/option/ability transactions

### P3 — Rule reuse

- Game.Shared dependency graph
- AuthoritativeSessionBase
- Mechanics
- Ability
- Effect
- Combat

### P4 — First real match

    Client A
       |
       +----+
            |
       Modern Server
            |
       +----+
       |
    Client B

只有 P0-P4 完成后，才考虑：

- Profile
- Deck collection
- Campaign
- Tournament
- Arena
- AI
- Shop
- Mail
- Auction
- RabbitMQ distributed workers

---

# 21. 构建

需要：

    .NET 10 SDK

执行：

    dotnet restore HexServer.Modern.sln
    dotnet build HexServer.Modern.sln -c Release
    dotnet test tests/HexServer.Protocol.Tests/HexServer.Protocol.Tests.csproj -c Release

当前执行环境没有 .NET SDK，因此本轮不会虚报本地编译结果。

GitHub Actions 是当前真实 CI 验证入口。

---

# 22. 最终运行形态

目标保持简单：

    HexServer.exe
    appsettings.json
    data/
    logs/

默认：

    HConnect 127.0.0.1:9933
    SQLite
    InMemory MessageBus

生产/多人扩展时再启用：

    RabbitMQ
    external workers
    telemetry
    background jobs

不会再次回到“启动十几个项目才能运行”的旧架构。

---

# 23. 一句话总结

**Modern 的核心不是写一个更大的服务器，而是把原 HEX 客户端已经知道的东西全部挖出来，然后只实现它当年真正缺失的那一半：Server。**
