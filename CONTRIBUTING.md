# 贡献指南

欢迎参与 ElasticBreath 项目的开发!ElasticBreath 是一款基于"水位预警模型"的 Windows WPF 生产力与健康提醒应用,通过边缘辉光与弹性时间窗口引导用户及时休息。无论是修复 Bug、完善功能、补充文档还是新增语言包,都非常欢迎你的贡献。

## 贡献指南概述

本项目以简体中文为主要工作语言,代码、文档与提交信息均优先使用中文。在开始任何改动前,建议先阅读以下文档以了解整体设计:

- `STATE_MACHINE.md`、`design.md`、`implementation.md`:状态机与整体设计说明
- `docs/i18n.md`:国际化语言包扩展指南
- `CHANGELOG.md`:版本演进记录

## 开发环境准备

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / 11(本项目依赖 WPF 与 Win32 P/Invoke) |
| SDK | .NET 8 SDK,或 .NET 9 SDK(由 `global.json` 锁定,`rollForward` 为 `latestMajor`) |
| IDE | Visual Studio 2022,或 Visual Studio Code(配合 C# Dev Kit) |

准备步骤:

1. 克隆仓库:

   ```bash
   git clone <repo-url> ElasticBreath
   cd ElasticBreath
   ```

2. 还原依赖并构建,验证本地环境可用:

   ```bash
   dotnet build ElasticBreath.sln
   ```

   若构建成功且无 error,则环境就绪。当前 `TreatWarningsAsErrors` 为 `false`,警告不会中断构建,但仍应尽量消除警告。

3. (可选)生成可移植发布包以验证发布流程:

   ```bash
   dotnet publish ElasticBreath.App/ElasticBreath.App.csproj -c Release -p:PublishProfile=Portable
   ```

   或使用打包脚本:

   ```powershell
   ./scripts/build-release.ps1
   ```

## 代码规范

本项目通过 `.editorconfig` 与 `Directory.Build.props` 强制代码风格,请遵循以下约定:

- **文件编码**:UTF-8(无 BOM)
- **换行符**:CRLF
- **缩进**:4 个空格
- **命名空间**:优先使用 `file_scoped` 命名空间
- **私有字段**:使用 `_camelCase` 命名
- **分析器**:`EnableNETAnalyzers` 已启用,`AnalysisLevel` 为 `latest-recommended`,`EnforceCodeStyleInBuild` 已开启

### XML 文档注释

对于面向领域/对外可见的代码,优先使用**简体中文**撰写 XML 文档注释,并与现有代码风格保持一致。请勿在方法体内部残留自动生成的 `///` 片段(例如 IDE 自动补全产生的空 `<summary>` 占位)。若某成员无需文档注释,直接省略即可,不要留下空壳。

### 分层与依赖

请遵循既有分层结构,不要跨层引用:

- `Domain/`:设置、枚举、快照等纯领域模型
- `Services/`:引擎、监视器、存储等服务
- `UI/`:WPF 窗口与界面逻辑
- `Interop/`:Win32 P/Invoke 封装

## 提交规范

本项目采用 [Conventional Commits](https://www.conventionalcommits.org/) 风格的提交信息,主体内容可使用中文。格式如下:

```
<type>: <简要描述>
```

常用 `type`:

| type | 用途 |
| --- | --- |
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `docs` | 文档变更 |
| `refactor` | 重构(不改变外部行为) |
| `test` | 新增或修改测试 |
| `chore` | 构建、脚本、依赖等杂项 |
| `perf` | 性能优化 |

示例:

```
feat: 新增全屏回退时的任务栏闪烁与提示音
fix: 修复 Win32Native.cs 中重复 DllImport 导致的构建中断
docs: 补充 i18n 语言包扩展指南
refactor: 抽取边缘辉光绘制逻辑为独立方法
test: 为 ExpressionEvaluator 添加单元测试
chore: 新增 GitHub Actions CI 工作流
perf: 降低轮询监视器在空闲时的 CPU 占用
```

提交信息应聚焦于"为什么"改动,而非单纯罗列"做了什么"。详细变更请同步更新 `CHANGELOG.md` 的 `[Unreleased]` 段落。

## 分支与 PR 流程

1. Fork 本仓库至个人账号。
2. 基于 `main` 创建特性分支,命名建议 `feat/<主题>` 或 `fix/<主题>`:
   ```bash
   git checkout -b feat/sound-reminder
   ```
3. 完成开发并提交。如涉及多步,可拆分为多个聚焦的提交。
4. 推送至你的 Fork,并向本项目 `main` 发起 Pull Request。
5. PR 要求:
   - **构建必须通过**:确保 `dotnet build ElasticBreath.sln` 无 error。
   - 在 PR 描述中说明改动内容、动机与影响范围;若涉及行为变更,注明测试方式。
   - 若改动涉及用户可见行为或界面,建议附上截图或录屏。
   - 同步更新相关文档与 `CHANGELOG.md`。
6. 维护者 review 通过后合并。

## 添加国际化语言包

如果你希望为 ElasticBreath 增加新的界面语言,请参阅 [`docs/i18n.md`](./docs/i18n.md),其中详细说明了语言包文件结构、键命名约定与验证清单。

## 报告问题

提交 Bug 报告或功能建议时,请尽量包含以下信息,以便快速定位:

- **版本**:从 `ElasticBreath.App/ElasticBreath.App.csproj` 中读取的版本号(当前为 `0.9.0`)。
- **操作系统**:Windows 版本(如 Windows 11 23H2)与显示缩放比例。
- **重现步骤**:最小可复现的操作序列,逐步说明。
- **预期行为**:你期望发生什么。
- **实际行为**:实际发生了什么,包括错误信息或异常现象。
- **崩溃日志**:若应用崩溃,请附上 `%TEMP%\ElasticBreath\crash\` 下对应的崩溃日志文件(由 `CrashLogger` 写入)。

## 行为准则

请保持友善、尊重的交流态度。针对问题与代码进行讨论,不针对个人。对不同经验水平的贡献者保持耐心,共同维护一个开放的协作环境。
