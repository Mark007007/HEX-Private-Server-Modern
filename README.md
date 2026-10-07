
# HEX Private Server Modern

现代化、低耦合的 HEX: Shards of Fate 本地/私服兼容服务器。

## 这一次的目标

不是重新做一个“类似 HEX”的游戏，而是最大限度利用原客户端已经保存的协议、数据契约和 Game.Shared 规则：

- HConnect
- DataWrapper / EncData / ObjFmt
- Service / Request / Response contracts
- Game.Shared.Session
- Game.Shared.AuthoritativeSessionBase
- Game.Shared.Mechanics
- Transactions
- SessionEventArgs

服务器基础设施采用现代 C# / .NET 10；原客户端 DLL 只作为受控的逆向 Oracle / 兼容参考，不作为最终服务器的运行时依赖。

## 当前核心状态

当前 main 已经形成第一条真实协议链：

    Original HEX Client
          |
          v
    HConnect TCP :9933
          |
          v
    HCP frame
          |
          v
    DataWrapper
          |
          v
    ObjFmt
          |
          v
    ServiceLoadBalancer (254)
          |
          +-- 22011 StartSession
          +-- 22015 FindSession
          +-- 22017 JoinSession
          |
          v
    GameSessionRegistry
          |
          v
    ObjFmt response

同时已经建立：

- HCP 半包/粘包 frame reader
- HCP ccnt/scnt 状态
- DataWrapper 五字段编码/解析
- ObjFmt primitive、UID、ResourceId、enum、list、nested object
- SessionState + 16 字段 EncounterData 编码基础
- GameSession request contracts
- Session mailbox / actor
- DLL HexExtractor
- Legacy shared-type load probe
- GitHub Actions 自动 restore/build/test

## 一个重要纠正

之前代码把 StartSession / FindSession / JoinSession 错归到了 ServiceGameSession。

已经修正为：

    ServiceLoadBalancer = 254

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

而：

    ServiceGameSession = 246

主要进入：

    3029 PlayerTransaction
    3050 PlayerAdded
    3051 PlayerRemoved
    3052 GameContinue
    3053 GameStarted
    3054 GameEnded
    3055 SessionSyncEvent
    3056 ChampionStatsUpdated

这个边界是后续真正接入原版战斗规则的基础。

## 逆向证据

核心记录：

    docs/REVERSE-ENGINEERING.md

重点不是“猜协议”，而是：

    CLI metadata / IL
          >
    runtime behaviour
          >
    golden bytes
          >
    decompiler presentation

尚未证实的加密算法、RabbitMQ 玩家协议、旧式 0x4858 包头不会被硬编码。

## CI 与历史错误

仓库现在使用 GitHub Actions：

    restore
    build
    test

之前出现过的失败已经通过 CI 日志逐项定位，包括：

- Solution restore 方式错误
- HexServer.Game.csproj 漏提交
- SessionMailbox 漏提交
- HcpFrame namespace 引用缺失
- .NET 10 numeric overload 歧义
- async iterator 中非法 yield / try-catch
- HCP service request 的 MethodId / DataType 混淆
- LoadBalancer 220xx / GameSession 30xx 边界错误
- SessionState.EncounterData 被错误简化

这些现在都已经进入后续修复链。

当前执行容器没有 .NET SDK，因此不会虚报“本地已经编译通过”；以最新 GitHub Actions head 的结果为准。

## 现在真正剩下的核心

还没有完成的，不是商店、拍卖或 Arena，而是：

1. 原客户端 auth:req 真实登录闭环
2. 完整 ObjFmt decoder：dictionary / generic / raw struct 等
3. ReadyForGameSetup 完整响应
4. 3055 SessionSyncEvent 完整编码
5. 3029 PlayerTransaction concrete transaction decoding
6. 最小 Game.Shared 规则依赖岛
7. 两个原版客户端进入同一个本地对战

## 最终目标

最终运行形态保持简单：

    HexServer.exe
    appsettings.json
    data/
    logs/

默认本地：

    HConnect 127.0.0.1:9933
    SQLite
    InMemory MessageBus

RabbitMQ、Tournament、Arena、Python AI 等都只能在证明需要时作为扩展加入，不能重新把服务器做成一堆互相依赖的旧式模块。

## 工具

HexExtractor 读取原始 .NET DLL 的 Type / Field / Method / Assembly Reference。

LegacyLoadProbe 检查原始 DLL 的 Game.Shared 类型是否能够被隔离加载。

这些工具用于最大化复用原客户端，而不是把完整 Unity 客户端搬进服务器。
