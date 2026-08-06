# ElasticBreath 状态机说明（当前实现）

## 状态定义
- `Idle`：空闲，未进入工作周期。
- `Working`：工作计时进行中。
- `Resting`：休息计时进行中。
- `Paused`：工作被离开检测中断后的暂停态。

## 全局原则
- 所有自动检测阈值均以秒计，来自设置页。
- 自动检测基于 `GetLastInputInfo`，并带远程控制前台进程过滤（ToDesk/RayLink 等）。
- 倒计时通知期间，如果再次无操作（针对 `Idle->Working`、`Paused->Working`），会自动取消本次切换。

## 详细转换规则

### Idle
- 手动点击“开始工作” -> `Working`，工作计时从 `0` 开始。
- 连续活动达到 `IdleToWorkDetectSeconds` 后，进入 `Idle->Working` 倒计时（`AutoTransitionCountdownSeconds`）。
- 倒计时结束 -> `Working`，并将工作计时起点设为 `IdleToWorkDetectSeconds`（检测时间计入步骤时间）。
- 倒计时期间若出现无活动（超过 `SmartDetectGapSeconds`）-> 倒计时取消，保持 `Idle`。

### Working
- 手动点击“开始休息”或角落触发 -> `Resting`，休息计时从 `0` 开始。
- 无操作达到 `AwayThresholdSeconds` -> 进入 `Working->Paused` 倒计时。
- `Working->Paused` 倒计时结束 -> `Paused`。
- 无操作达到 `AutoRestAfterIdleSeconds` -> 直接进入 `Resting`，且休息计时起点设为 `AutoRestAfterIdleSeconds`（检测时间计入步骤时间）。
- 手动“停止” -> `Idle`。

### Paused
- 连续活动检测到输入 -> 进入 `Paused->Working` 倒计时。
- `Paused->Working` 倒计时结束 -> `Working`（延续原工作周期计时，不清零）。
- 倒计时期间若出现无活动（超过 `SmartDetectGapSeconds`）-> 倒计时取消，保持 `Paused`。
- 手动“停止” -> `Idle`。

### Resting
- 手动点击“开始工作”或角落触发 -> `Working`，工作计时清零重启。
- 连续密集输入达到 `RestToWorkDetectSeconds` -> 自动进入 `Working`，并将工作计时起点设为 `RestToWorkDetectSeconds`（检测时间计入步骤时间）。
- 手动“停止” -> `Idle`。

## 会话事件
- `SessionLock`（锁屏）：强制进入 `Idle`，并清零 `WorkingCycleElapsed`、`RestingCycleElapsed` 与空闲活动探测时长，标记本次休息为有效，同时清除任何待处理的状态切换（锁屏时直接切换到 idle，避免解锁后状态检测异常）。
- `SessionUnlock`（解锁）：保持当前状态，仅发布最新快照，后续按输入检测继续流转。

## 推迟（Postpone）机制
- 推迟仅在 `Working` 且压力为 `Warning`/`Hard` 时可用（`Safe` 区及非工作状态不可推迟）。
- 推迟成功后进入冷却期，时长为 `PostponeCooldownSeconds`（默认 5 分钟）。冷却期内不再因空闲触发"工作→休息"自动切换（手动切换不受影响）。
- 每日推迟上限为 `DailyPostponeLimit`（默认 3 次），达到上限后当日不可再推迟；跨自然日自动重置为满额。
- "完整休息"定义：休息时长 ≥ 最短有效休息时长（`MinEffectiveRestSeconds`，默认 3 分钟）。仅完成一次完整休息后，今日推迟配额重置为满额。
- 运行时状态（已用次数、冷却计时）仅存内存，重启后配额恢复为满额。
- 触发方式：主窗口"推迟"按钮。推迟成功会清除因空闲产生的待处理"工作→休息"切换。

## 角落触发规则
- 鼠标在任一角落连续停留达到 `CornerHoverSeconds` 即触发一次切换（工作<->休息）。
- 触发后必须先离开四个角落区域，才允许下一次触发。

## 视觉行为（实现层）
- `Working`：按工作时长进入 `Safe/Warning/Hard`。
- `Resting`：按休息时长进入 `Base/Elastic/Overtime`。
- `Overtime` 区间下，边缘渗透宽度会逐步增长到配置上限。
