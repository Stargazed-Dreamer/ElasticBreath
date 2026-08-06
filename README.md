# 弹性呼吸 (ElasticBreath)

## 项目简介

弹性呼吸（ElasticBreath）是一款面向长时间电脑使用者的弹性休息引导工具。它摒弃了传统的"倒计时炸弹"模型，转而采用"水位预警模型"，通过边缘视觉与弹性时间窗口，为使用者提供不可习惯化的休息引导，同时绝不强制打断关键操作。

本应用基于 C# / WPF / .NET 8 构建，通过 Win32 P/Invoke 与系统交互。核心设计原则是：永不阻断中心视野——无模态弹窗、不抢焦点、不黑屏；悬浮提示没有"关闭"出口，只能"完成"；以弹性时间窗口替代硬性闹钟；并具备双向智能感知（自动识别离开/归来）、渐进压迫以及环境与状态适配（单屏极简 / 多屏增强 / 全屏隐匿）等特性。

## 核心特性

- **弹性时间窗口**：以弹性时间窗口替代硬性闹钟，避免在关键操作时被强制打断。
- **水位预警模型**：摒弃"倒计时炸弹"模型，采用水位渐进式预警，提供不可习惯化的休息引导。
- **边缘呼吸光晕**：通过低 CPU 的 Win32 分层窗口在屏幕边缘绘制呼吸光晕与顶部进度条，仅作用于边缘视觉，永不阻挡中心视野。
- **角落悬停触发**：支持将鼠标悬停在屏幕角落以主动触发休息。
- **双向智能感知**：自动识别用户离开与归来，无需手动启停。
- **多屏自适应**：单屏极简、多屏增强、全屏隐匿三种模式自动适配当前显示环境。
- **中英双语**：基于 JSON 的国际化方案，内置 zh-CN 与 en-US 语言包，从 `i18n/` 目录自动发现。
- **便携免安装**：单文件便携版，不写注册表、无需安装，配置以可读的算术表达式存储。

## 截图

<!-- 截图占位 -->

## 系统要求

- 操作系统：Windows 10 / 11 64-bit
- 运行时：.NET 8 运行时（使用自包含发布版时无需单独安装运行时）

## 快速开始

**方式一：从源码构建运行**

```bash
git clone <repo-url> ElasticBreath
cd ElasticBreath
dotnet build ElasticBreath.sln
dotnet run --project ElasticBreath.App/ElasticBreath.App.csproj
```

**方式二：下载便携版**

从 Releases 页面下载便携版 zip，解压后直接运行 `ElasticBreath.exe`，无需安装，不写注册表。

## 配置说明

配置文件位于 `config/settings.json`，所有时长字段均以可读的算术表达式存储，例如 `"35*60"` 表示 35 分钟（2100 秒），保持配置文件的人类可读性，便于审阅与手动调整。

常用配置项也可通过应用内的设置界面（`SettingsWindow`）修改，保存时会自动写回 `config/settings.json`。

## 项目结构

```
ElasticBreath/
├── ElasticBreath.App/
│   ├── Domain/                  # 领域模型：设置、状态枚举、不可变引擎快照
│   │   ├── ElasticBreathSettings.cs
│   │   ├── ElasticBreathState.cs
│   │   └── EngineSnapshot.cs
│   ├── Services/                # 服务层：状态机、输入监测、本地化、设置存储等
│   │   ├── BreathEngine.cs                   # 休息引导状态机
│   │   ├── InputMonitor.cs                   # 基于 GetLastInputInfo 的空闲检测
│   │   ├── CornerTriggerService.cs           # 角落悬停触发
│   │   ├── DisplayTargetService.cs           # 显示目标 / 多屏适配
│   │   ├── LocalizationService.cs            # JSON 国际化
│   │   ├── SettingsStore.cs                  # 设置持久化（算术表达式）
│   │   ├── SessionMonitor.cs                 # 会话 / 登录状态监测
│   │   ├── SecondaryMonitorFlashService.cs   # 副屏闪烁增强
│   │   ├── RemoteInputFilterService.cs       # 远程控制工具过滤
│   │   ├── ExpressionEvaluator.cs            # 算术表达式求值
│   │   └── CrashLogger.cs                    # 崩溃日志（仅本地）
│   ├── UI/                      # UI 层：各类 WPF 窗口
│   │   ├── EdgeOverlayWindow.xaml(.cs)              # 边缘呼吸光晕分层窗口
│   │   ├── CountdownNotificationWindow.xaml(.cs)    # 倒计时通知
│   │   ├── ToastWindow.xaml(.cs)                    # 悬浮提示（无"关闭"出口，只能"完成"）
│   │   ├── SettingsWindow.xaml(.cs)                 # 设置界面
│   │   └── HelpWindow.xaml(.cs)                     # 帮助界面
│   ├── Interop/                 # Win32 P/Invoke 互操作
│   │   └── Win32Native.cs
│   ├── i18n/                    # 国际化语言包（自动发现）
│   │   ├── zh-CN.json
│   │   └── en-US.json
│   └── Properties/
│       └── PublishProfiles/
│           └── Portable.pubxml  # 便携单文件发布配置
├── scripts/
│   └── build-release.ps1        # 发布构建脚本
├── design.md                    # 设计文档
├── STATE_MACHINE.md             # 状态机说明
├── implementation.md            # 实现说明
└── ElasticBreath.sln
```

## 构建与发布

构建解决方案：

```bash
dotnet build ElasticBreath.sln
```

发布便携单文件版本：

```bash
dotnet publish ElasticBreath.App/ElasticBreath.App.csproj -c Release -p:PublishProfile=Portable
```

也可使用项目内置的发布脚本：

```powershell
./scripts/build-release.ps1
```

## 文档导航

- [设计文档](design.md)
- [状态机说明](STATE_MACHINE.md)
- [实现说明](implementation.md)
- [用户指南](USER_GUIDE.md)
- [架构说明](ARCHITECTURE.md)
- [贡献指南](CONTRIBUTING.md)
- [更新日志](CHANGELOG.md)
- [安全策略](SECURITY.md)

## 贡献

欢迎参与贡献，请先阅读 [贡献指南](CONTRIBUTING.md)。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

Copyright (c) 2026 ElasticBreath contributors
