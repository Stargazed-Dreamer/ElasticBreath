# 变更日志

本项目所有显著变更均记录于此文件。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/),版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

本次维护阶段正在进行中的改动。

### Added

- 项目基础设施:新增 `global.json`(锁定 SDK 版本,`rollForward` 为 `latestMajor`)、`Directory.Build.props`(启用 .NET 分析器,`AnalysisLevel` 为 `latest-recommended`)、`.editorconfig`(统一 UTF-8 / CRLF / 4 空格缩进 / `file_scoped` 命名空间 / `_camelCase` 私有字段)、可移植发布配置 `Portable` 发布配置文件、发布打包脚本 `scripts/build-release.ps1`。
- 补齐缺失模块(进行中):声音提醒、全屏回退时的任务栏闪烁与提示音、下次启动时的崩溃恢复提示、角落悬停倒计时环视觉、推迟配额系统。
- 新增测试项目 `ElasticBreath.Tests`(xUnit)。
- 新增 CI/CD GitHub Actions 工作流。
- 新增文档:README、LICENSE(MIT)、CONTRIBUTING、CHANGELOG、ARCHITECTURE、USER_GUIDE、SECURITY、i18n 指南。

### Changed

- _本次维护阶段暂无显著变更。_

### Fixed

- 修复 `Interop/Win32Native.cs` 中重复 `DllImport` 声明导致的构建中断。

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
