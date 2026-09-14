# host-station

通用工业上位机（C# + WPF / .NET）。

## 技术栈

- 语言 / UI：C# + WPF (.NET)
- 一期协议：串口、TCP、Modbus TCP/RTU
- 架构：协议适配层 + 采集缓冲队列与 UI 解耦

## 分工（代码工作群）

| 角色 | 范围 |
|------|------|
| selyla | 协议适配层、采集缓冲队列（多设备、背压、断线重连） |
| wayly | 主窗口、设备/点位、实时曲线、告警列表 |
| herry | 解决方案脚手架、单测、CI（build+test）、发布骨架 |
| him | 采集吞吐与背压策略、压测 |
| strlla | PR / issue 审核，对齐 CI 口径 |
| weli | 任务拆分与验收 |

## 状态

仓库已创建，脚手架与业务代码由团队陆续推入。


## 协议 / 采集（selyla）

- `HostStation.Core/Acquisition`：`AcquisitionSession` / `AcquisitionHub`（多设备轮询 → 有界队列）
- `HostStation.Core/Buffering`：背压策略 DropOldest / DropNewest / Block
- `HostStation.Core/Reconnect`：指数退避重连
- `HostStation.Protocols`：Serial / TCP 传输桩、Modbus 适配器、RTU CRC / TCP MBAP 组帧

```bash
dotnet test HostStation.sln -c Release
```

## 背压 / 吞吐（him）

- `BackpressureProfile`：RealtimeUi / ReliableAlarms / SoakDropNewest
- `DeviceRateLimiter`：单设备令牌桶，防热设备饿死共享队列
- `BoundedSampleQueue`：可观测 `Dropped` / `Enqueued`
- 文档：`docs/backpressure.md`
- 压测：`dotnet test --filter Backpressure`

