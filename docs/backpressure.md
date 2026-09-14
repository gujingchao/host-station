# 采集吞吐与背压（him）

## 目标

多设备轮询灌入共享队列时，UI 跟不上也不能把进程拖死；告警路径不能默默丢关键点。

## 策略

| Profile | 队列 | 溢出 | 单设备上限 | 用途 |
| --- | --- | --- | --- | --- |
| `RealtimeUi` | 2048 | DropOldest | 200 sps | 曲线 / 点位刷新 |
| `ReliableAlarms` | 4096 | Block | 50 sps | 告警 / 审计 |
| `SoakDropNewest` | 64 | DropNewest | 1000 sps | 压测看 shedding |

实现：

- `BoundedSampleQueue`：DropOldest / DropNewest / Block，暴露 `Dropped` / `Enqueued`
- `DeviceRateLimiter`：按设备令牌桶，防止单设备打爆共享队列
- `BackpressureProfile`：推荐组合

## 压测口径（对齐 CI）

```bash
dotnet test tests/HostStation.Core.Tests --filter Backpressure
```

断言：

1. 突发写入下 DropOldest 丢旧保新，且 `Dropped > 0`
2. DropNewest 丢新，队列内仍是旧样本
3. Block 时生产者被拖慢，不丢样本
4. 多生产者 + 限流：热设备 `Rejected > 0`，冷设备仍有样本进队
5. 吞吐下限：单测环境短跑 enqueued/s 不低于门槛（防回归）
