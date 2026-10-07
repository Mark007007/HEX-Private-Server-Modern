# HEX Private Server Modern

Modern .NET 10 reimplementation of the HEX private-server compatibility layer.

## 核心路线

- 最大化复用原 HEX 客户端的 Game.Shared 规则核心。
- 按原客户端恢复 HConnect / DataWrapper / EncData / ObjFmt 协议。
- 现代化重写传输、服务宿主、持久化和工具链。
- 默认模块化单体；RabbitMQ 作为可选 MessageBus。
- 开发阶段使用原版逻辑作为 Oracle，支持差分与 Replay。

## 当前状态

已建立第一阶段 .NET 10 工程骨架，并开始实现真实 HCP 帧、可靠连接、Service ID / Method ID、Session Mailbox 和 Server Adapter。

详细逆向证据见 `docs/REVERSE-ENGINEERING.md`。

## 协议原则

不要猜包头、加密算法或 RabbitMQ 玩家协议。只有经 DLL 元数据、IL 或动态 capture 证实的行为才进入正式协议实现。

## 许可证与客户端文件

原 HEX 客户端 DLL 和数据文件的再分发权与本项目源码许可证相互独立；请仅使用你有权使用的数据。
