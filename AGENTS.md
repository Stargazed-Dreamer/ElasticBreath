# 维护规范（AGENTS.md）

本文件面向人类贡献者与 AI 编程助手，列出维护 ElasticBreath 时必须遵守的硬性规则。新增提交、PR 或自动生成代码前，请先通读本文件。

相关上下文文档：
- 架构与技术债：[`ARCHITECTURE.md`](ARCHITECTURE.md)
- 安全与隐私：[`SECURITY.md`](SECURITY.md)
- 状态机：[`STATE_MACHINE.md`](STATE_MACHINE.md)
- 贡献流程：[`CONTRIBUTING.md`](CONTRIBUTING.md)
- 设计意图：[`design.md`](design.md)

---

## 一、隐私优先（最高优先级）

ElasticBreath 定位为本地运行、零遥测的隐私优先工具。源码、文档、CI、脚本、提交历史均不得泄露任何个人或开发机信息。

### 1.1 禁止入库的内容

下列内容不得进入版本库。已通过 `.gitignore` 排除的（如 `todo.txt`、`bin/`、`obj/`、`config/settings.json`、`crash/`）**不得 unignore**；新增开发机产物应加入 `.gitignore` 而非提交，也无需删除既有文件——用 ignore 控制即可。

- **他人**的个人信息：他人真实姓名、邮箱、电话、QQ/微信、GitHub 用户名等。未经本人同意不得在仓库中具名。
- 维护者**非项目用途**的敏感数据：个人手机号、私人邮箱（非项目联系渠道）、个人服务器地址、个人域名解析记录等。
- 开发机绝对路径（如 `C:\Users\<某人>\`、`F:\codex\...`、`/home/<某人>/`）。所有路径必须为相对路径，或通过 `Path.GetTempPath()` / `AppContext.BaseDirectory` 在运行时拼接。
- 任何 API key、token、密码、证书、签名密钥。
- 开发机私有产物：`*.lnk`、`*.url`、`*.user`、`*.suo`、IDE 缓存、私人 TODO 备忘（`todo.txt`）。
- 崩溃日志、运行时生成的 `config/settings.json`、`crash/` 目录内容。

> 说明：维护者本人的项目署名（GitHub handle `Stargazed-Dreamer`）不在此列，见 §1.2。

### 1.2 装配与项目元数据

- 维护者本人可在公开元数据中个人署名：`csproj` 的 `<Authors>` 为 `Stargazed-Dreamer`，`<Copyright>` 为 `Copyright © Stargazed-Dreamer`；LICENSE 版权行为 `Copyright (c) 2026 Stargazed-Dreamer`。`<Company>` / `<Product>` 保留为 `ElasticBreath`。
- 其他贡献者如需在 `<Authors>` / LICENSE / README 致谢中具名，须征得本人同意；默认不维护具名贡献者列表。
- 不得在元数据中填入他人邮箱、手机号等敏感联系方式。

### 1.3 Git 提交历史

- 维护者身份（`Stargazed-Dreamer`）是**有意公开**的项目署名，**不重写历史以移除维护者身份**。如维护者个人偏好使用 noreply 邮箱可自行选择，不作强制。
- 既有提交历史保留不动；不为了"清理"旧提交信息（如 `AI补注释`）而重写历史。
- **今后新提交**遵循 [Conventional Commits](https://www.conventionalcommits.org/)（`feat`/`fix`/`docs`/`refactor`/`test`/`chore`/`perf`），不得包含开发机绝对路径、他人个人信息或 AI 辅助痕迹等过程性表述。

### 1.4 运行时数据

- 崩溃日志仅写入 `Path.GetTempPath()/ElasticBreath/crash/`，**绝不**联网上传；用户主动提交日志的入口需在 UI 中明示，由用户显式复制。
- 任何"统计/上报/分析"代码均不得引入；新增 NuGet 依赖前需确认其不含遥测组件。
- `GetLastInputInfo`、`GetForegroundWindow` 等只读型系统调用不得升级为读取按键内容、窗口文本、进程内存；如需新的系统读取能力，先在 `SECURITY.md` 中补充边界说明。
- 不引入任何网络请求代码（无 HttpClient、无 WebSocket 客户端、无自动更新检查），更新检查由用户主动访问 Release 页面完成。

### 1.5 文档与 i18n

- README、CONTRIBUTING、SECURITY、帮助窗口文案、i18n 语言包示例字符串中，禁止出现个人联系信息或真实用户数据。占位符统一使用 `your-email@example.com` 等通用形式。
- 致谢/贡献者名单如需公开，须征得当事人同意；默认不在仓库内维护具名贡献者列表。
- `SECURITY.md` 的漏洞上报指引已改为分流：普通 Bug / 功能建议走公开 Issue，安全漏洞走 GitHub 私密 Security Advisory；不引入联系邮箱。后续维护保持该分流策略。

---

## 二、避免平台绑定

ElasticBreath 当前为 Windows 专属（`net8.0-windows` + WPF + WinForms + Win32 P/Invoke）。**为保留未来跨平台（Linux/macOS）的可能，新增代码必须遵守以下规则**，不得加深平台耦合。

### 2.1 P/Invoke 单一表面

- **所有** Win32 P/Invoke（`[DllImport]`）只能声明在 [`ElasticBreath.App/Interop/Win32Native.cs`](ElasticBreath.App/Interop/Win32Native.cs) 中，禁止散落到 UI 或 Services 层。
- 新增 P/Invoke 前，先评估是否能用 .NET 跨平台 API 替代（`System.Diagnostics.Process`、`System.Environment`、`System.IO` 等）。
- 每个新增 P/Invoke 必须在文件顶部注释区登记用途，并同步在 `SECURITY.md` 中补充"读取了什么、不读取什么"的边界说明。

### 2.2 服务层抽象

新增需要 OS 能力的服务时，**必须先定义接口**，再在 `Interop/` 或 `Services/` 下提供 Windows 实现：

```csharp
// 推荐：接口定义在 Services/ 或 Domain/
public interface IInputMonitor { /* ... */ }

// Windows 实现放在 Interop/ 或 Services/ 下，命名带 Windows 后缀或置于 #if WINDOWS 分支
internal sealed class WindowsInputMonitor : IInputMonitor { /* ... */ }
```

待抽象的现有边界（参考 `ARCHITECTURE.md` §11 技术债）：
- `IInputMonitor`（系统空闲时间、光标位置）
- `IDisplayTargetService`（多屏枚举、全屏前台检测）
- `ISessionMonitor`（锁屏/解锁事件）
- `ISoundPlayer`（PCM 播放，替代 `System.Media.SoundPlayer`）
- `ITrayIcon`（系统托盘，替代 WinForms `NotifyIcon`）
- `IOverlayWindow`（分层窗口渲染，替代 `UpdateLayeredWindow`）

在接口存在前，**禁止**在 UI 层直接调用上述能力的 Win32/WinForms API；应先抽接口再使用。

### 2.3 禁止扩大 WinForms 依赖

- 不得新增对 `System.Windows.Forms` 的依赖。现有 `NotifyIcon`、`Screen`、`Cursor.Position`、`ContextMenuStrip` 已记入技术债，仅允许在现有位置维护，不得扩散到新文件。
- 多屏枚举、托盘、剪贴板等能力的新写法应走抽象接口，或在 WPF 内寻找等价 API（如 `System.Windows.Clipboard`）。
- `csproj` 中 `<UseWindowsForms>true</UseWindowsForms>` 不得在新增项目里复制；新项目优先用纯 WPF 或纯 .NET。

### 2.4 路径与文件系统

- 禁止硬编码 `C:\`、`F:\`、`%APPDATA%`、`%LOCALAPPDATA%`、`/home/`、`/tmp/` 等绝对或平台专属路径。
- 配置目录统一用 `AppContext.BaseDirectory` 拼接 `config/`；临时目录统一用 `Path.GetTempPath()`。
- `Environment.SpecialFolder` 仅使用 .NET 已做跨平台映射的成员（如 `LocalApplicationData`），不得假设其值等于某个 Windows 路径。

### 2.5 保持纯逻辑层纯净

下列文件是**纯逻辑、无平台依赖**的核心，禁止引入 WPF/WinForms/Win32 using：

- `Domain/*`（`ElasticBreathSettings`、`ElasticBreathState`、`EngineSnapshot`）
- `ElasticBreath.Rendering/*`（`EdgeOverlayPixelRenderer.cs` 等，目标框架 `net8.0`，已被主项目与离线截图工具共用，是跨平台渲染核心）
- `Services/BreathEngine.cs`（除 `DispatcherTimer` 外不得新增 WPF 依赖；理想状态是替换为 `System.Threading.Timer`）
- `Services/PostponeService.cs`、`Services/ExpressionEvaluator.cs`、`Services/LocalizationService.cs`、`Services/SettingsStore.cs`、`Services/CrashLogger.cs`、`Services/CrashRecoveryService.cs`、`Services/CornerTriggerService.cs`
- 全部 `ElasticBreath.Tests/*`

这些是未来跨平台移植时可直接复用的资产，**任何向其注入平台耦合的改动都应被 PR 拒绝**。如需时间源，使用可注入的 `Func<DateTime>`（参考 `PostponeService`）；如需定时器，使用接口注入而非直接 `new DispatcherTimer()`。

### 2.6 条件编译与运行时分支

- 若必须为不同平台写不同实现，优先用**接口 + 平台专属实现类**，而非 `#if WINDOWS` 散布于业务代码中。
- 仅在互操作边界（`Interop/`）内部允许 `RuntimeInformation.IsOSPlatform` 检查。
- `csproj` 若未来改为多目标（`net8.0;net8.0-windows`），平台专属文件用条件 `<Compile Include="...Condition="...">` 控制，不要在共享文件中堆 `#if`。

### 2.7 构建与发布脚本

- 新增构建脚本优先用 `pwsh`（PowerShell Core 跨平台）或 `bash`，避免 `.bat`。
- 发布配置（`*.pubxml`）按 RID 拆分（`win-x64`、`linux-x64`、`osx-arm64`），不要在单一 pubxml 中硬编码单一 RID。
- CI 工作流（`.github/workflows/`）保持矩阵化结构，便于加入 `ubuntu-latest` / `macos-latest` runner；不要在 step 中硬编码 `windows-latest` 专属命令。

### 2.8 命名空间与类型选用

- 几何类型：`Services` 层避免 `System.Windows.Rect/Point`，改用自定义值类型或 `System.Drawing.Rectangle`（仅在已是 WinForms 依赖的文件中）。新写的纯逻辑文件使用自定义 `readonly struct` 表示矩形/点。
- 集合与并发：用 `System.Collections.Concurrent` / `System.Collections.Generic`，不要用 `System.Windows.Threading.DispatcherTimer` 之外的 WPF 集合类型进入 Services 层。

---

## 三、架构与代码组织

- 分层：`Domain`（纯数据/规则）→ `Services`（业务逻辑 + 抽象接口）→ `UI`（WPF 渲染）→ `Interop`（Win32 单一表面）。新文件按此归类，不得跨层循环依赖。
- 不可变快照：状态经 `EngineSnapshot` 单向流出，UI 不得回写状态字段；新状态字段加在 `EngineSnapshot` 上，由 `BreathEngine` 在 Tick 末构造。
- 异常守卫：所有未捕获异常经 `App.xaml.cs` 三层守卫落盘到 `CrashLogger`，**不得**在业务代码中 `throw` 后无人接管；新增异步代码须确保 `UnobservedTaskException` 可观测。
- MainWindow god-class 是已知技术债（见 `ARCHITECTURE.md` §11）。新增服务组合逻辑优先考虑独立 Service 类，不要继续往 `MainWindow.xaml.cs` 堆叠。

---

## 四、测试与质量门禁

- 所有 `Services/` 与 `Domain/` 的行为改动须有对应单元测试（xUnit，位于 `ElasticBreath.Tests/`）。
- 测试不得依赖 Win32/WPF/WinForms；涉及时间的逻辑用可注入 `Func<DateTime>` 推进虚拟时钟（参考 `PostponeServiceTests`）。
- 涉及状态机的改动需更新 `STATE_MACHINE.md` 中的转换表。
- 涉及设置的改动需更新 `ElasticBreathSettings.Sanitize()` 与对应字段 min/max 常量，并在 `SettingsSanitizeTests` 中补充边界用例。
- 表达式配置字段（支持 `35*60` 这种写法）须经 `ExpressionEvaluator.TryEvaluate` 求值，并在 `SettingsStore` 中保留原文往返；新增此类字段需同步更新 `SettingsWindow` 的实时校验。
- CI（`.github/workflows/build.yml`）会在每个 PR 上跑 `dotnet test`，本地提交前请运行 `dotnet build` 与 `dotnet test` 确保通过。

---

## 五、文档同步清单

提交涉及以下方面时，必须同步更新对应文档，避免文档与代码漂移：

| 改动类型 | 需同步的文档 |
|---|---|
| 新增/修改 Win32 P/Invoke | `SECURITY.md`、`ARCHITECTURE.md` §7 |
| 状态机转换规则 | `STATE_MACHINE.md` |
| 新增设置字段 | `USER_GUIDE.md`、`ElasticBreathSettings.Sanitize`、i18n 语言包 |
| 新增 i18n 键 | `docs/i18n.md`、`zh-CN.json` 与 `en-US.json` 同步 |
| 架构/技术债变化 | `ARCHITECTURE.md` §11 |
| 用户可见行为变化 | `CHANGELOG.md`（Keep a Changelog 格式） |
| 隐私/数据收集边界变化 | `SECURITY.md` |
| 维护规则变化 | 本文件（`AGENTS.md`） |

---

## 六、AI 助手特别约定

面向 AI 编程助手（含本文件的主要读者）的额外约束：

- **不要**主动创建新的 `.md` 文档除非用户明确要求；优先更新现有文档。
- **不要**在源码中添加"由 AI 生成"之类的注释或提交信息。
- **不要**为提升"工程质量"而引入 DI 容器、MVVM 框架、日志框架等大改动；这类重构需先在 Issue 中讨论并写入 `ARCHITECTURE.md` §11 技术债清单。
- **不要**把 `Win32Native.cs` 中的 P/Invoke 内联到调用方"为了少一层跳转"；单一表面是规则不是建议。
- **修改 `EdgeOverlayPixelRenderer` 或任何 Layered Window 像素写入路径前，必读 [`ARCHITECTURE.md`](ARCHITECTURE.md) §5.4。** `UpdateLayeredWindow` 用 `AC_SRC_ALPHA`（premultiplied）模式，像素缓冲的 RGB 必须乘以 `alpha/255`；若写成 straight alpha，实机会出现"渐变消失、透明度一样"的 bug（而离线 `DemoRenderer` 截图却正常，极具迷惑性）。`DemoRenderer.CompositeOnto` 必须同步保持 premultiplied over 语义。
- 修改 `Services` 层时，若不确定是否引入平台耦合，先 grep 文件中是否已存在 `System.Windows`/`System.Drawing`/`Microsoft.Win32`/`ElasticBreath.App.Interop` using；若不存在，新增即违规。
- 涉及个人信息的占位值（邮箱、姓名、路径）一律使用本文件 §1.5 列出的通用形式，不要"编造一个看起来真实的"示例。
- 提交前自查：`git diff` 中是否出现个人路径、个人邮箱、AI 辅助痕迹、`Console.WriteLine` 调试残留、`.lnk`/`.user` 等开发机产物。如有，停止提交并修正。
