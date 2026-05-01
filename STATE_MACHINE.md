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
- `SessionLock`（锁屏）：强制进入 `Resting`。
- `SessionUnlock`（解锁）：保持当前状态，后续按输入检测继续流转。

## 角落触发规则
- 鼠标在任一角落连续停留达到 `CornerHoverSeconds` 即触发一次切换（工作<->休息）。
- 触发后必须先离开四个角落区域，才允许下一次触发。

## 视觉行为（实现层）
- `Working`：按工作时长进入 `Safe/Warning/Hard`。
- `Resting`：按休息时长进入 `Base/Elastic/Overtime`。
- `Overtime` 区间下，边缘渗透宽度会逐步增长到配置上限。
