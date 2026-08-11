# 变更日志

本项目所有显著变更均记录于此文件。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/),版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

本次维护阶段正在进行中的改动。

### Added

- 项目基础设施:新增 `global.json`(锁定 SDK 版本,`rollForward` 为 `latestMajor`)、`Directory.Build.props`(启用 .NET 分析器,`AnalysisLevel` 为 `latest-recommended`)、`.editorconfig`(统一 UTF-8 / CRLF / 4 空格缩进 / `file_scoped` 命名空间 / `_camelCase` 私有字段)、可移植发布配置 `Portable` 发布配置文件、发布打包脚本 `scripts/build-release.ps1`。
- 补齐缺失模块(进行中):声音提醒、全屏回退时的任务栏闪烁与提示音、下次启动时的崩溃恢复提示。
- 新增测试项目 `ElasticBreath.Tests`(xUnit)。
- 新增 CI/CD GitHub Actions 工作流。
- 新增文档:README、LICENSE(MIT)、CONTRIBUTING、CHANGELOG、ARCHITECTURE、USER_GUIDE、SECURITY、i18n 指南。
- 新增 `ElasticBreath.DemoRenderer` 离线截图生成工具:复用渲染核心生成 README 展示用图,背景为合成渐变壁纸,无任何真实桌面/任务栏/窗口内容,零隐私风险。支持 `--bg <路径>` 指定自定义背景图测试不同壁纸下的效果,输出文件名追加 `--<basename>` 后缀避免覆盖默认版本。
- 新增 `docs/screenshots/`:六种状态 PNG、顶部进度条五联对比 PNG、Warning 脉冲 GIF,由 DemoRenderer 生成,像素级与实机一致。
- 新增 `scripts/render-demo.ps1`:展示截图生成脚本入口,封装构建与运行流程,支持 `-Bg`/`-Out`/`-NoBuild` 参数,便于多图测试。
- 新增“关于”窗口(`UI/AboutWindow.xaml` / `.xaml.cs`)与元数据读取服务 `Services/AppMetaService.cs`:版本、作者、仓库链接、许可证摘要与简介在窗口打开时从应用基目录下的 `app.meta.json` 实时读取;`app.meta.json` 通过 csproj `<Content Include>` 复制到输出目录。仓库链接以 WPF `Hyperlink` 呈现,点击通过系统 shell 委托给默认浏览器打开(程序自身不发起 HTTP 请求),旁附“复制地址”按钮;另提供“复制版本信息”(版本+作者+仓库+许可+OS+运行时,便于反馈 bug 时附上下文)与“打开崩溃日志文件夹”(指向 `%TEMP%\ElasticBreath\crash\`)两个便捷入口。元数据字段缺失时对应行自动折叠,文件不可用时全部回退为空值而不抛异常。同步新增 15 个 `about.*` i18n 键(zh-CN/en-US 对称),更新 `docs/i18n.md` 键总数与命名约定表,补充 `SECURITY.md` 关于系统浏览器边界的说明。
- 关于窗口新增应用图标显示:从输出目录下的 `Resource/icon.ico` 读取多帧图标,选取像素宽度最大(最清晰)的帧,以 `Stretch="Uniform"` + `BitmapScalingMode="HighQuality"` 缩放到 56×56 容器内,保证按比例缩放不裁切。
- `AppMetaService` 新增程序集属性回退:`app.meta.json` 缺失、为空(0 字节)或解析失败时,`version`/`copyright`/`shortDescription` 三个字段回退到程序集属性(csproj 的 `<InformationalVersion>`/`<Copyright>`/`<Description>`),保证元数据文件被破坏时关于界面仍能显示版本号而非“版本未知”。`authors`/`repository`/`license` 无对应程序集属性,缺失时由 UI 折叠对应行。同步新增 6 个测试用例覆盖回退场景。
- 关于窗口移除“简介”行(信息与 README 重复且占空间);“复制地址”按钮移至仓库链接下方一行,避免 URL 较长时与按钮并排溢出(窗口保持 480 宽)。
- 重写“功能说明”窗口内容与渲染:从单一 TextBlock 升级为 FlowDocument,小标题段(`【...】`/`[...]`开头)加粗着色,正文段常规,段落间有间距,阅读层次清晰。内容全面重写为基于 USER_GUIDE/STATE_MACHINE 真实行为:新手 30 秒上手、四种状态说明、关键设计(没有关闭按钮)、智能感知(离开/归来/锁屏/远程控制)、值得探索的特性(算式输入/多屏/全屏/推迟机制)、理想使用场景、数据与隐私。zh-CN/en-US 对称同步。

### Changed

- 抽取边缘覆盖层像素渲染逻辑到独立项目 `ElasticBreath.Rendering`(`EdgeOverlayPixelRenderer`):主应用 Layered Window 与离线截图工具共用同一份渲染代码,保证实机与截图像素级一致;`EdgeOverlayWindow.xaml.cs` 改为委托调用,删除原内联的 `FillPixels`/`DrawProgressBar`/`BlendPixel`/`ResolveVisual`。

### Fixed

- 修复 `Interop/Win32Native.cs` 中重复 `DllImport` 声明导致的构建中断。
- 修复边缘光晕渐变失效(实机表现为"透明度一样、渐变消失"):`UpdateLayeredWindow` 使用 `AC_SRC_ALPHA`(premultiplied alpha)模式,但 `EdgeOverlayPixelRenderer.Render` 写入的是 straight alpha(RGB 未乘以 alpha),导致 alpha 变化对最终显示颜色几乎无影响。现统一输出 premultiplied BGRA,四边渐变从边缘高亮平滑过渡到背景色;同步更新 `BlendPixel`(premultiplied over 操作)与 `DemoRenderer.CompositeOnto`(premultiplied over),保证实机与离线截图一致。
- 修复"暂停提醒"按钮误切到空闲态:`BreathEngine.SetRemindersPaused(true)` 原实现调用 `StopToIdle()`,导致界面显示"空闲"且清零周期计时(而状态机中不存在"工作中无操作自动转空闲"的规则,空闲会在用户操作后自动转回工作)。现改为无论当前状态如何均进入"暂停"状态并记住暂停前状态;点击"恢复提醒"时回到暂停前状态(工作→工作、休息→休息、其余回空闲)。同步更新引擎注释与 4 个状态机测试用例。
- 功能说明窗口恢复"关键设计"中遗漏的角落悬停说明(新手误删):补齐"把鼠标移到屏幕任一角停留约 1.5 秒,出现倒计时圆环填满即切换"及"触发后需移出角落区域才能再次触发"两条,zh-CN/en-US 对称同步。
- 重新设计角落悬停触发为**仅左上角**并新增视觉指示圆 `UI/CornerIndicatorWindow.xaml(.cs)`:圆心位于屏幕左上角(用户可见右下角四分之一)的半透明实心圆,进入角落时弹性胀大到半径 20px(短暂过冲再回弹),悬停期间颜色由灰(`#7F7F7F` 55%)渐变为主题绿(`#32B265` 80%),变绿瞬间完成切换,切换后保持绿色直到鼠标移出,移出后弹性收回。窗口置顶、点击穿透、不抢焦点,由 `MainWindow` 250ms 角落轮询驱动 `ShowAt`/`Retract`。右上角(邻近关闭按钮)、右下角("显示桌面"热区)、左下角(开始菜单)一律不再触发。
- 同步清理角落相关表述:`CornerTriggerService.DetectCorner` 仅返回 "LT",`GetHoverProgress` 触发后保持进度 `1.0` 使圆保持绿色直到鼠标移开;i18n 更新 `hint.corner`、`settings.corner_hover_seconds`(左上角停留触发时长)、`settings.enable_corner_hover`(启用左上角悬停触发)与 help.body 角落说明(zh-CN/en-US 对称);更新 USER_GUIDE/STATE_MACHINE/README/ARCHITECTURE/design.md 中角落触发相关描述;`CornerTriggerServiceTests` 改为仅左上角用例并新增"其他角落不触发""触发后保持绿色直到移开"用例。
- 修复左上角指示圆不可见:`CornerIndicatorWindow` 原用 WPF Storyboard(`Begin(this, true)`)驱动动画,但动画在窗口首次 `Show()` 的同一调用栈内启动、视觉树尚未就绪时会静默失败,导致 `Opacity`/`Scale` 停留在初始 0 值,圆完全不可见。改为 `DispatcherTimer` 逐帧手动驱动(与 `EdgeOverlayWindow` 同一模式),每帧直接设置缩放与透明度,并保留弹性胀大/收回与灰→绿渐变效果。

## [1.0.0] - 2026-08-11

首个正式版本。在 0.9.0 基线之上完成功能裁剪与体验收敛。

### Added

- 主界面新增"关于"按钮入口(位于"功能说明"右侧),为后续"关于"窗口预留接入点,当前暂未实现具体行为。

### Changed

- 版本号统一升级为 `1.0.0`(`Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion`)。
- 设置窗口默认尺寸收紧至与最小尺寸一致(`600×520`),开启即为紧凑形态,不再首次开启过大。
- 更新"功能说明"文案:补充角落悬停 1.5 秒触发的具体行为说明,标注当前版本号 1.0.0。

### Removed

- 移除"推迟"功能:删除 `PostponeService`、`EngineSnapshot.Postpone` 字段、`BreathEngine.TryPostpone()`、主界面"推迟"按钮及相关 i18n 键与单元测试。自动休息触发不再受推迟冷却限制。
- 移除角落悬停倒计时圆环视觉(`CornerRingWindow`):角落悬停触发功能保留,但不再渲染进度圆环。删除 `CornerRingWindow.xaml` / `.xaml.cs` 及 `MainWindow` 中的接线。

## [0.9.0] - 2026-08-06

本次维护阶段之前的基线快照,记录已有的全部功能。

### Added

- 状态机:实现 `Idle` / `Working` / `Resting` / `Paused` 四态流转,作为"水位预警模型"的核心调度框架。
- 边缘辉光与顶部进度条:通过分层窗口(layered window)在屏幕边缘绘制水位辉光与顶部进度条,直观反映剩余可工作时长。
- 角落悬停触发:鼠标悬停在屏幕指定角落可触发休息倒计时。
- 多显示器自适应:主显示器绘制辉光,副显示器同步闪烁,适配多屏办公场景。
- 会话锁定处理:在系统锁定/解锁时正确暂停与恢复计时。
- 双语国际化:内置 `zh-CN`(简体中文)与 `en-US`(English)两套语言包,各 117 个键,对称一致。
- 算术表达式设置:数值型配置项以原始算术表达式存储(如 `35*60`),便于阅读,由 `SettingsStore` 配合 `ExpressionEvaluator` 解析。
- 设置窗口与帮助窗口:提供图形化设置入口与帮助说明窗口。
- 托盘图标:系统托盘图标支持快捷操作与状态展示。
- 崩溃日志:`CrashLogger` 将崩溃信息写入 `%TEMP%\ElasticBreath\crash\`。

[_0.9.0 作为本次维护阶段之前的基线快照保留。_]
