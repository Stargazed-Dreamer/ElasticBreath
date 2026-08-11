# ElasticBreath 架构说明

本文档面向开发者与维护者，描述 ElasticBreath 的分层结构、核心机制、Win32 互操作策略以及已知技术债务。状态机的完整转换规则请参阅 [STATE_MACHINE.md](STATE_MACHINE.md)，产品级设计规格见 [design.md](design.md)。

## 1. 架构总览

ElasticBreath 是一个 Windows WPF（.NET 8）单进程应用，采用经典的“分层 + 编排者”结构。所有逻辑围绕一个不可变快照（`EngineSnapshot`）流转，UI 仅负责渲染，不存在反向数据绑定（当前未引入 MVVM）。

```
┌──────────────────────────────────────────────────────────────────────┐
│  App.xaml.cs  ── 全局异常守卫（Dispatcher / AppDomain / TaskScheduler）│
│        ↓ bootstrap                                                    │
│  MainWindow.xaml.cs  ── 编排者（god-class）：组合服务、托盘、轮询、渲染  │
│        │                                                              │
│        ├── 周期 DispatcherTimer：1s 引擎 Tick / 250ms 角落轮询          │
│        │                                                              │
│        ▼  订阅 SnapshotChanged 事件                                    │
│  ┌─────────────────────── Services ───────────────────────────────┐    │
│  │ BreathEngine (1s DispatcherTimer, 状态机, 发布 EngineSnapshot)  │    │
│  │   ├─ InputMonitor        (GetLastInputInfo + DenseInput 跟踪)   │    │
│  │   │    └─ RemoteInputFilterService (前台进程名匹配远程工具)     │    │
│  │   ├─ CornerTriggerService (左上角悬停 1.5s + 再触发门控)         │    │
│  │   ├─ DisplayTargetService   (preferred→primary→cursor 回退)     │    │
│  │   ├─ SecondaryMonitorFlashService (副屏 EdgeOverlayWindow 闪烁) │    │
│  │   ├─ SessionMonitor   (Microsoft.Win32.SystemEvents.SessionSwitch)│   │
│  │   ├─ SettingsStore     (JSON + 算术表达式往返)                  │    │
│  │   ├─ ExpressionEvaluator(递归下降 + - * / ( ))                   │    │
│  │   ├─ LocalizationService(JSON i18n, T/Tf)                       │    │
│  │   └─ CrashLogger       (%TEMP%\ElasticBreath\crash\*.log)       │    │
│  └────────────────────────────────────────────────────────────────┘    │
│        │                                                              │
│        ▼  状态/参数驱动渲染                                            │
│  ┌──────────────────────── UI (WPF Window) ────────────────────────┐    │
│  │ EdgeOverlayWindow         ── HWND 复用为 Win32 Layered Window    │    │
│  │   (UpdateLayeredWindow + 手写 byte* 像素填充, 200ms 动画定时器)  │    │
│  │ CountdownNotificationWindow ── 右上角非模态倒计时卡片           │    │
│  │ ToastWindow / SettingsWindow / HelpWindow                       │    │
│  └────────────────────────────────────────────────────────────────┘    │
│        │                                                              │
│        ▼                                                              │
│  Interop/Win32Native.cs ── P/Invoke 表面                              │
│  (GetLastInputInfo / GetCursorPos / GetForegroundWindow / GetWindowRect │
│   GetWindowThreadProcessId / GetWindowLongPtr / SetWindowLongPtr       │
│   SetWindowPos / UpdateLayeredWindow / CreateDIBSection / ...)         │
└──────────────────────────────────────────────────────────────────────┘
```

**数据流（核心）**：`BreathEngine` 持有一个 1 秒间隔的 `DispatcherTimer`。每次 Tick 中，引擎采样输入、推进计时、处理自动状态切换，然后构造一份**不可变**的 `EngineSnapshot` record，通过 `SnapshotChanged` 事件发布。`MainWindow` 订阅该事件，把快照映射为 UI 状态：圆环进度、压力等级文案、覆盖层颜色与厚度、倒计时通知卡片等。这是单向、纯数据的流——UI 不向引擎回写状态，只调用引擎的命令方法（`StartWorkingManual` / `StartRestingManual` / `TriggerCornerTransition` / `CancelPendingTransition` / `HandleSessionSwitch` …）。

## 2. 分层职责

### 2.1 Domain/（领域模型，纯 POCO/record，无依赖）

| 文件 | 职责 |
| :--- | :--- |
| `ElasticBreathSettings.cs` | 配置 POCO。所有数值以秒为单位的 `int`/`double` 存储；`Sanitize()` 方法对各字段做 `Math.Clamp` 并处理字段间依赖（如 `MaxWorkSeconds ≥ MinWorkSeconds`、`AwayThresholdSeconds > RestOvertimeSeconds`）；暴露一组 `TimeSpan` 计算属性（`MinWorkThreshold` 等）便于引擎消费；`RawExpressions` 字典保存数值字段的原始算术表达式文本以支持持久化往返。`CloseBehavior` 枚举定义主窗口关闭行为（退出 / 收起至托盘）。 |
| `ElasticBreathState.cs` | 状态枚举：`ElasticBreathState`（`Idle`/`Working`/`Resting`/`Paused`）、`WorkingPressureLevel`（`Safe`/`Warning`/`Hard`）、`RestPressureLevel`（`Base`/`Elastic`/`Overtime`）、`PendingTransitionKind`（五种待处理切换）。 |
| `EngineSnapshot.cs` | 不可变 record，引擎每 tick 发布。包含状态、压力等级、循环/今日累计、`PendingTransitionSnapshot?`、`DetectionProbeSnapshot?`、暂停/锁屏标志、暂停前状态、时间戳。提供 `WorkingProgressRatio` / `RestingProgressRatio` 计算进度比。 |

### 2.2 Services/（业务服务，无 UI 依赖，可独立测试）

| 文件 | 职责 |
| :--- | :--- |
| `BreathEngine.cs` | 核心状态机。1s `DispatcherTimer`；`OnTick` 推进计时、判定自动切换、推进待处理切换倒计时；通过 `SnapshotChanged` 发布 `EngineSnapshot`。提供手动命令与角落切换、会话切换处理。 |
| `InputMonitor.cs` | 调用 `Win32Native.GetIdleDuration` 采样系统空闲时长，并跟踪 `DenseInputDuration`（密集输入持续时长，用于 `RestToWork` 判定）。注入 `RemoteInputFilterService` 以在远程工具前台时抑制误判。返回 `InputSample`。 |
| `CornerTriggerService.cs` | 左上角悬停检测。`CornerHitSize = 18` 像素判定区（仅左上角）；`TryTrigger` 返回是否达成 `hoverDuration`；`_mustExitCornerBeforeNextTrigger` 门控确保触发后必须先离开左上角区域才允许下一次触发，防止连发；`GetHoverProgress` 输出悬停进度供指示圆视觉使用，触发后保持 `1.0` 使圆保持绿色直到鼠标移开。 |
| `DisplayTargetService.cs` | 选定目标屏幕：首选显示器（按 `DeviceName` 匹配）→ 主屏 → 光标所在屏。`IsFullscreenForeground` 通过 `GetForegroundWindow` + `GetWindowRect` 与目标屏边界（容差 2px）比较，判定是否被全屏应用覆盖。 |
| `SecondaryMonitorFlashService.cs` | 多屏增强。为每个非主屏 `Screen` 懒创建一个 `EdgeOverlayWindow`，当主屏浮层被忽略（`primaryScreenIgnored`）时以 700ms 周期闪烁副屏边框；实现 `IDisposable` 统一释放。 |
| `SessionMonitor.cs` | 包装 `Microsoft.Win32.SystemEvents.SessionSwitch`，通过 `SessionLockChanged` 事件向引擎报告锁屏/解锁。 |
| `SettingsStore.cs` | 配置持久化。`Load`/`Save` 读写 exe 同级 `config/settings.json`；加载时把字符串型算术表达式求值后替换回 JSON 节点以便反序列化，同时把原文保留到 `RawExpressions`；保存时反向把 `RawExpressions` 写回对应字段，保持文件人类可读。 |
| `ExpressionEvaluator.cs` | 递归下降解析器，支持 `+ - * / ( )` 与一元正负号、整数/小数；`TryEvaluate` 返回 `double` 与错误标识（`empty`/`invalid_char`/`nan_or_inf`/`divide_zero`/`syntax`）。 |
| `LocalizationService.cs` | JSON 国际化加载器。从 `i18n/*.json` 自动发现可用语言；`T(key)` 查找（缺失则返回 key 本身），`Tf(key, args)` 调用 `string.Format`；语言文件缺失时回退到 `zh-CN`。 |
| `RemoteInputFilterService.cs` | 远程控制工具过滤。维护已知进程名集合（`todesk`/`raylink`/`sunloginclient`/`sunlogin`/`teamviewer`/`anydesk`/`rustdesk`）；`IsLikelyRemoteControlForeground` 取前台窗口 PID → 进程名匹配。 |
| `CrashLogger.cs` | 静态崩溃日志写入器，输出到 `%TEMP%\ElasticBreath\crash\crash-yyyyMMdd-HHmmss-fff.log`，记录时间、来源、OS、运行时、异常消息与完整堆栈；内部吞掉自身异常以避免日志失败二次崩溃。 |

### 2.3 UI/（WPF 窗口）

| 文件 | 职责 |
| :--- | :--- |
| `EdgeOverlayWindow.xaml(.cs)` | 边缘光晕与顶部进度条。WPF `Window` 的 HWND 在 `SourceInitialized` 时通过 `Win32Native.SetLayered` 转为 Win32 Layered Window，绕过 WPF 渲染管线。200ms `DispatcherTimer` 驱动脉冲/闪烁；像素填充（颜色表、四边渐变、顶部进度条）委托给 [`ElasticBreath.Rendering.EdgeOverlayPixelRenderer`](#25-elasticbreathrendering纯渲染逻辑无平台依赖)，本类只负责 HWND/DIB Section 生命周期与 `UpdateLayeredWindow` 提交。`EdgeOverlayState` 枚举迁移至 `ElasticBreath.Rendering` 命名空间，映射压力等级到视觉态。 |
| `CountdownNotificationWindow.xaml(.cs)` | 右上角非模态卡片，显示待处理切换类型与剩余秒数；任意位置点击触发 `CancelRequested` 事件，由 `MainWindow` 转发到 `engine.CancelPendingTransition()`。 |
| `ToastWindow.xaml(.cs)` | 滑入式提示窗口（角落切换成功等瞬时反馈）。 |
| `CornerIndicatorWindow.xaml(.cs)` | 左上角悬停指示圆。置顶、点击穿透、不抢焦点的透明小窗，圆心位于屏幕左上角（用户可见右下角四分之一）；进入角落时弹性胀大到半径 20px，悬停期间颜色由灰（`#7F7F7F` 55%）渐变到主题绿（`#32B265` 80%），切换后保持绿色直到鼠标移出，移出后弹性收回。`MainWindow` 的 250ms 角落轮询驱动 `ShowAt`/`Retract`。 |
| `SettingsWindow.xaml(.cs)` | 模态设置对话框，按“时间参数 / 交互与检测 / 视觉参数 / 显示 / 声音与通知”分组；输入框支持算术表达式，保存时调用 `ExpressionEvaluator` 校验并写入 `RawExpressions`。 |
| `HelpWindow.xaml(.cs)` | 简易功能说明窗口。 |

### 2.4 Interop/（Win32 P/Invoke）

| 文件 | 职责 |
| :--- | :--- |
| `Win32Native.cs` | 唯一的 P/Invoke 表面。输入：`GetLastInputInfo`/`GetCursorPos`/`GetForegroundWindow`/`GetWindowRect`/`GetWindowThreadProcessId`；窗口样式与定位：`GetWindowLongPtr`/`SetWindowLongPtr`/`SetWindowPos`/`ForceTopmost`/`SetClickThroughNoActivate`/`SetLayered`/`SetWindowBoundsPixels`；Layered Window 渲染：`CreateDIBSection`(`CreateArgbBitmap`)/`UpdateLayeredWindow`(`RenderLayeredWindow`)/GDI 对象销毁。 |

<a id="2.5-elasticbreathrendering纯渲染逻辑无平台依赖"></a>

### 2.5 ElasticBreath.Rendering/（纯渲染逻辑，无平台依赖）

独立项目（`net8.0`，无 WPF/WinForms/Win32 using），从 `EdgeOverlayWindow` 抽取的纯像素渲染逻辑。同时被主应用的 Layered Window 与离线截图工具 `ElasticBreath.DemoRenderer` 引用，保证实机与截图像素级一致。

| 文件 | 职责 |
| :--- | :--- |
| `EdgeOverlayPixelRenderer.cs` | 静态类。`EdgeOverlayState` 枚举（`Hidden`/`Warning`/`Hard`/`RestBase`/`RestElastic`/`RestOvertime`/`Paused`）映射压力等级到视觉态；`EdgeOverlayVisual` record struct 承载颜色、透明度因子、脉冲周期、闪烁标记；`ResolveVisual` 产出视觉参数；`ComputeOpacityFactor` 计算某时刻的脉冲/闪烁权重；`Render` 在 BGRA 缓冲上绘制四边渐变光晕与顶部进度条，`DrawProgressBar` 居中 360×10 距顶 8px（仍硬编码，见 §11），`BlendPixel` 处理角落叠加。 |

`ElasticBreath.DemoRenderer`（`net8.0-windows` + `System.Drawing.Common` NuGet，未启用 `UseWindowsForms`）是离线控制台工具，调用上述渲染器生成 `docs/screenshots/` 下的展示图：6 种状态 PNG、顶部进度条五联对比 PNG、Warning 脉冲 GIF。背景统一为合成渐变壁纸，无任何真实桌面/任务栏/窗口内容。

## 3. 核心机制

### 3.1 状态机 + 快照模式

引擎状态为 `Idle` / `Working` / `Resting` / `Paused`，完整转换规则见 [STATE_MACHINE.md](STATE_MACHINE.md)。要点：

- **Idle → Working**：连续活动累积达到 `IdleToWorkDetectSeconds` 后调度待处理切换；倒计时结束后工作计时起点设为 `IdleToWorkDetectThreshold`（检测时间计入工时）。
- **Working → Resting**：手动 / 角落触发立即切换；或无操作达到 `AutoRestAfterIdleSeconds` 后调度待处理切换，切换后休息计时起点设为 `AutoRestAfterIdleThreshold`。
- **Resting → Working**：手动 / 角落触发；或密集输入达到 `RestToWorkDetectSeconds` 后自动切换。若休息时长短于 `MinEffectiveRestSeconds` 且未被判定有效，则工作计时向前回滚（休息前工时 + 本次休息时长），即“无效休息不重置工时”。
- **Paused → Working**：检测到活动后调度倒计时；倒计时结束延续原工作周期（不清零）。
- **SessionLock**：强制进入 `Idle` 并清零周期计时；解锁后按输入检测恢复。

### 3.2 1 秒 Tick + 待处理切换倒计时 + 取消

`OnTick` 的固定顺序：`ResetDailyCountersIfNeeded` → 采样输入 → `AdvanceCycleTime` → `HandleAutomaticTransitions` → `HandlePendingTransition` → `PublishSnapshot`。

自动切换不是瞬时执行：`SchedulePendingTransition` 创建一个 `PendingTransition`（类型、消息键、`AutoTransitionCountdownSeconds` 剩余），并在后续每个 Tick 中：

1. **条件复检**：根据切换种类检查采样是否仍满足触发条件（如 `IdleToWorking` 需持续活动、`WorkingToPaused` 需持续离开）。条件不满足则**取消**该待处理切换，回到原状态。
2. **倒计时递减**：每 Tick 减 1 秒。
3. **到期执行**：剩余 ≤ 0 时执行实际状态切换。

UI 通过 `EngineSnapshot.PendingTransition` 拿到剩余秒数与消息键，渲染为右上角倒计时卡片；用户点击卡片即调用 `CancelPendingTransition()`。

`DetectionProbeSnapshot` 用于在无待处理切换时显示“还需持续 X 秒才会触发”的探测进度，提升智能感知的可预期性。

## 4. 线程模型

**单 UI 线程**。没有任何 `async`/`await`，也没有后台工作线程。原因：

- 所有状态都属 UI 范畴，引擎计时本身就是秒级粒度，不需要并发。
- Layered Window 像素填充必须在创建 HWND 的线程上下文提交；统一在 UI 线程处理避免跨线程 GDI 问题。
- 配置读写、i18n 加载发生在启动与设置保存两个低频点，无需异步化。

`DispatcherTimer` 驱动三组节拍：

| 定时器 | 间隔 | 用途 | 所在 |
| :--- | :--- | :--- | :--- |
| 引擎 Tick | 1 s | 推进计时、状态切换、发布快照 | `BreathEngine` |
| 覆盖层动画 | 200 ms | 脉冲/闪烁帧、调用 `RenderLayeredWindow` | `EdgeOverlayWindow` |
| 角落轮询 | 250 ms | `CornerTriggerService.TryTrigger` | `MainWindow` |

另有可选的“周期性重新置顶”定时器（默认 5 s，受 `EnablePeriodicReTopmost` 控制）防止 `Win+D` 等操作把浮层推到 Z 序后面。

所有渲染（像素填充、`UpdateLayeredWindow`）都是同步调用，但成本极低（详见 §10）。`OnTick` 内部无任何阻塞 I/O。

## 5. Win32 互操作策略

### 5.1 为什么用 Layered Window + 手写像素

`EdgeOverlayWindow` 是一个 WPF `Window`，但在 `SourceInitialized` 时立刻调用 `Win32Native.SetLayered`，给 HWND 加上 `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`：

- `WS_EX_LAYERED`：交由 `UpdateLayeredWindow` 直接提交 32 位 ARGB 位图，绕过 WPF 的保留模式渲染树，GPU 几乎不参与，CPU 仅做一次像素填充。
- `WS_EX_TRANSPARENT`：鼠标点击穿透到下层窗口，光晕不影响任何交互。
- `WS_EX_TOOLWINDOW`：不出现在任务栏 / Alt+Tab。
- `WS_EX_NOACTIVATE`：永不抢焦点，符合“不阻断中心视野”的设计红线。

每帧由 `EdgeOverlayPixelRenderer.Render`（`unsafe byte*`）填充四边渐变（上/下/左/右各做线性 alpha 衰减，角落用 `BlendPixel` 做 alpha 叠加避免硬边），再由 `DrawProgressBar` 绘制居中顶部进度条，最后 `RenderLayeredWindow` 一次性提交。

### 5.2 P/Invoke 表面

集中在 `Win32Native.cs`，分三组：输入采样、窗口样式与定位、Layered Window 渲染。新增 P/Invoke 应统一加在此处，避免散落到 UI 层。

### 5.3 AllowUnsafeBlocks 说明

`.csproj` 开启 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`，原因是 `EdgeOverlayPixelRenderer.Render` / `BlendPixel` / `DrawProgressBar` 使用 `byte*` 直接操作 `CreateDIBSection` 返回的非托管像素内存指针，以避免每帧 `Marshal.Copy` 托管数组的开销。这是达到设计性能红线（§10）的关键。

### 5.4 premultiplied alpha 约定（避坑，必读）

**这是最容易踩的坑，单独成节。** 任何修改 `EdgeOverlayPixelRenderer` 或新增 Layered Window 像素路径的人，必须先读完本节。

#### 现象

实机运行时，四边光晕"透明逐渐变浅的效果消失，透明度一样"——边缘到中心颜色几乎恒定，看不出渐变。但离线 `DemoRenderer` 生成的 PNG/GIF 渐变完全正常。

#### 根因

`Win32Native.RenderLayeredWindow` 调用 `UpdateLayeredWindow` 时，`BlendFunction.AlphaFormat = AC_SRC_ALPHA`（=1）。这告诉 GDI：**位图的 RGB 已经是 premultiplied alpha**（即 `RGB_premul = RGB_straight * alpha / 255`），混合公式为：

```
Result.RGB = Source.RGB_premul + Dest.RGB * (1 - Source.Alpha/255)
```

但 `EdgeOverlayPixelRenderer.Render` 早期版本写入的是 **straight alpha**（RGB 保持原值，未乘 alpha）。此时：

- `alpha` 从 255 衰减到 1 时，`Source.RGB_premul` 项被错误地保持为满值（255,83,83），只有 `(1 - alpha/255)` 这一项在变；
- 最终 `Result.RGB ≈ (255,83,83) + Dest * (1 - 1/255) ≈ (255,83,83)`，颜色几乎不变 → 渐变消失。

`DemoRenderer` 之所以"正常"，是因为它走的是 `System.Drawing.Bitmap`（`Format32bppArgb`）+ 自实现的 `CompositeOnto`，用的是 straight alpha 公式 `out = dst*(1-a) + src*a`，恰好与 straight alpha 输入匹配。两条路径用了不同的混合语义，导致"截图对、实机错"。

#### 硬性约定

1. **`EdgeOverlayPixelRenderer.Render` 的输出必须是 premultiplied BGRA。** 任何写入 `pixels[offset..offset+3]` 的代码，RGB 必须乘以 `alpha/255`：
   ```csharp
   var a = (byte)(alpha * ratio);
   var pr = (byte)(r * a / 255);
   var pg = (byte)(g * a / 255);
   var pb = (byte)(b * a / 255);
   pixels[offset]     = pb;  // B
   pixels[offset + 1] = pg;  // G
   pixels[offset + 2] = pr;  // R
   pixels[offset + 3] = a;   // A
   ```
2. **`BlendPixel`（角落叠加）必须做 premultiplied over 操作**：`outA = srcA + dstA*(1-srcA/255)`，`outRGB_premul = srcRGB_premul + dstRGB_premul*(1-srcA/255)`。
3. **`DrawProgressBar` 的背景与填充同样要 premultiply**，不能因为是"半透明深色底"就偷懒写 straight 值。
4. **任何新增的像素写入路径（进度条、角标、闪烁层等）都必须遵守上述约定。** Review 时若看到 `pixels[offset] = b;`（直接写原色）而旁边 `pixels[offset+3] = a;`（alpha 是变量），即可判定违规。

#### 与 DemoRenderer 的一致性

`DemoRenderer.CompositeOnto` 必须使用 premultiplied over 公式与 `Render` 输出匹配：

```csharp
var inv = 255 - a;
background[i] = (byte)Math.Min(255, overlay[i] + (background[i] * inv) / 255);
```

两条路径共用同一份 `EdgeOverlayPixelRenderer`，是保证"实机像素 == 截图像素"的前提。改 `Render` 时务必同步检查 `CompositeOnto`，反之亦然。

#### 验证方法

- **像素级验证**：实机运行触发 `Warning`/`Hard` 状态后截屏，用 Python/PIL 读取屏幕边缘像素。正常情况下，从 `y=0` 到 `y=glowThickness`，RGB 分量应**平滑衰减**到背景色（例如红色光晕从 ~(84,42,42) 衰减到 ~(23,23,23)）。若边缘到中心 RGB 几乎不变（如全是 (255,83,83)），即为 premultiplied 退化。
- **probe.log**：`overlay=Nx(skip=0)/Xms` 表示覆盖层正在活跃渲染；若 `overlay=0x` 持续，说明引擎未进入 `Working` 状态或覆盖层未被触发，与 alpha 问题无关。
- **单元测试**：当前 `EdgeOverlayPixelRenderer` 无单元测试（纯像素逻辑，断言成本高）。若新增测试，断言应检查 `pixels[offset] * 255 ≈ r * pixels[offset+3]`（premultiplied 不变式）。

## 6. 设置持久化

配置文件位于 exe 同级 `config/settings.json`（`SettingsStore` 构造时 `Directory.CreateDirectory` 保证存在）。首次启动若文件不存在，会写入经 `Sanitize()` 处理的默认值。

**算术表达式往返**：所有数值字段（`minWorkSeconds`、`maxWorkSeconds`、`defaultRestSeconds`、`restOvertimeSeconds`、`minEffectiveRestSeconds`、`awayThresholdSeconds`、`autoRestAfterIdleSeconds`、`idleToWorkDetectSeconds`、`restToWorkDetectSeconds`、`smartDetectGapSeconds`、`autoTransitionCountdownSeconds`、`cornerHoverSeconds`、`glowMaxThicknessPixels`、`overlayOpacity`、`reminderVolumePercent`、`reTopmostIntervalSeconds`）允许以字符串形式存储原文，例如 `"35*60"` 表示 35 分钟。

- **加载**：检测到字符串值且非纯数字时，记入 `RawExpressions`，并用 `ExpressionEvaluator.TryEvaluate` 求值替换回 JSON 节点，再交给 `JsonSerializer` 反序列化。
- **保存**：序列化后再次遍历 JSON，把 `RawExpressions` 中的非纯数字原文写回对应字段。

这样用户在设置界面输入 `35*60` 后，配置文件里始终保留 `35*60` 而非 `2100`，便于人工审阅与手动调整。任何加载异常都会被 `catch` 并回退到默认 `Sanitize()` 后的设置，保证应用可启动。

## 7. 国际化

`LocalizationService` 在构造时定位 `AppContext.BaseDirectory/i18n/`。`AvailableLanguages` 自动枚举该目录下所有 `*.json`（按文件名排序）。`Load(language)`：

1. 解析语言代码，空值视为 `zh-CN`。
2. 若目标文件不存在，回退到 `zh-CN.json`；若 `zh-CN.json` 也不存在，清空字典并记录当前语言。

`T(key)` 在字典中命中即返回译文，否则**返回 key 本身**作为兜底（避免 UI 空白）。`Tf(key, args)` 先 `T` 再 `string.Format`。键约定为点分命名空间（如 `notify.working_to_resting`、`settings.min_work`），与 `zh-CN.json` / `en-US.json` 一致。语言切换后写入 `settings.Language`，重启保留。

## 8. 多显示器与 DPI

**像素坐标系**：浮层定位全部走 Win32 物理像素（`SetWindowBoundsPixels` 接收 `System.Drawing.Rectangle`），刻意绕过 WPF 的设备无关单位，避免在 DPI 缩放下出现浮层与屏幕边界错位。

**目标屏回退链**（`DisplayTargetService.GetTargetScreen`）：
1. 用户在设置中指定的 `PreferredDisplay`（按 `Screen.DeviceName` 精确匹配，匹配成功即永久覆盖）；
2. 否则取系统主屏（`Screen.Primary`）；
3. 都不可用时回退到光标当前所在屏（`Screen.FromPoint(cursor)`）。

**全屏检测**：`IsFullscreenForeground` 比较前台窗口矩形与目标屏边界（容差 2 像素），命中则触发“全屏隐藏”或任务栏闪烁回退（见 design.md §7）。

**副屏闪烁**：`SecondaryMonitorFlashService` 为每个非主屏懒创建独立的 `EdgeOverlayWindow`，在主屏浮层被忽略（`primaryScreenIgnored`）时以 700ms 周期切换显隐，制造“副屏边框呼吸”效果，光晕厚度默认取 `GlowMaxThicknessPixels / 3`（夹紧到 8–36）。

**热插拔**：`Screen.AllScreens` 在每次更新时实时枚举，因此插拔显示器会自动适配；多变单时副屏窗口会被显式 `hideAll` 隐藏，无残留闪烁。

## 9. 异常与崩溃保护

`App.xaml.cs` 构造函数挂载三个全局处理器：

| 处理器 | 覆盖范围 | 处理方式 |
| :--- | :--- | :--- |
| `DispatcherUnhandledException` | UI 线程未处理异常 | `CrashLogger.Write` 后 `e.Handled = true`，阻止应用终止 |
| `AppDomain.CurrentDomain.UnhandledException` | 非 UI 线程 / 整个域的未处理异常 | 记录后无法阻止进程终止 |
| `TaskScheduler.UnobservedTaskException` | 未观察的 Task 异常 | 记录后 `e.SetObserved()` |

`CrashLogger.Write` 写入 `%TEMP%\ElasticBreath\crash\crash-yyyyMMdd-HHmmss-fff.log`，包含 ISO 8601 时间、来源标识、OS、运行时版本、异常消息与完整 `ToString()` 堆栈。日志器自身异常被静默吞掉，避免日志失败引发二次崩溃。

**恢复提示**：下次启动时应用可检测到 crash 目录下存在新日志（具体提示流程由上层在 `MainWindow` 启动时实现），向用户说明上次崩溃并指引查看日志。崩溃日志仅写本地临时目录，不上传任何远端。

## 10. 性能边界

- **光晕帧率**：动画定时器 200ms 间隔，即 **≈5 FPS**，远低于设计红线 ≤15 FPS。
- **渲染路径**：GDI `UpdateLayeredWindow`（**非** Direct2D）。两者技术路线不同但都属低功耗意图；本实现选择 GDI 是为了完全控制像素布局并避免引入 Direct2D 依赖。
- **跳帧优化**：`RenderFrame` 缓存上一帧的颜色与 alpha，若位图未脏且颜色未变则直接返回，不调用 `UpdateLayeredWindow`。
- **CPU 红线**：设计目标整体 CPU 占用 < 0.2%。
- **位图重分配**：仅在屏幕尺寸变化（`SetBounds` 改变）时重建 DIB Section，正常 tick 复用同一缓冲区。

## 11. 已知技术债务

以下问题已被识别，列入后续重构计划，本节如实记录以便评估与跟进：

- **`MainWindow.xaml.cs` 是 god-class**：当前约 **793 行**，混合了约 16 个关注点（服务组合、托盘图标、角落轮询、快照渲染、覆盖层协调、通知协调、设置应用、本地化应用、会话事件转发、窗口生命周期、显示器选择、全屏检测、副屏闪烁触发、退出流程等）。计划后续抽取纯逻辑 Helper（渲染映射、显示器决策）、独立的 Tray 服务，并迁移到 MVVM（引入 ViewModel 与双向绑定），届时 `MainWindow` 仅承担视图与命令路由。
- **无 DI 容器**：所有服务在 `MainWindow` 构造函数中 `new` 出来，依赖关系硬编码。这导致服务难以单独测试，也使上面提到的 god-class 拆分更困难。引入 DI 容器是 MVVM 重构的前置工作。
- **暂无单元测试**：当前没有测试项目。由于引擎与多数服务无 UI 依赖，已具备可测试性；计划新增 `ElasticBreath.Tests` 项目，优先覆盖 `BreathEngine` 状态机、`ExpressionEvaluator`、`SettingsStore` 表达式往返、`CornerTriggerService` 再触发门控。
- **WinForms + WPF 混用**：`UseWindowsForms=true`，通过 `System.Windows.Forms` 使用 `NotifyIcon`（托盘）、`Screen`（多屏枚举）、`Cursor.Position`（光标定位）。两套 UI 栈并存带来少许心智负担，但避免了重写托盘与屏幕枚举的成本，短期保留。
- **`RemoteInputFilterService` 仅匹配进程名**：通过前台窗口进程名匹配已知远程控制工具（ToDesk / RayLink / SunLogin / TeamViewer / AnyDesk / RustDesk），无法真正区分 RDP / VM 的输入来源是否来自宿主。即非完整意义上的“远程输入源过滤”，只是启发式抑制。
- **顶部进度条宽度硬编码**：`EdgeOverlayPixelRenderer.DrawProgressBar` 中 `barWidth = 360`（像素）、`barHeight = 10`、`barY = 8` 为字面量，未随屏幕分辨率或 DPI 自适应。在高分辨率宽屏上偏窄，未来应改为按屏幕宽度比例计算。
- **设计书中尚未落地的设置项**：design.md §8 中的“推迟冷却时长”与“每日推迟上限”尚未在 `ElasticBreathSettings` 中实现，相关推迟配额逻辑也未进入引擎；本节如实标注以避免与设计书产生认知偏差。
