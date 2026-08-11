<p align="center">
  <img width="64" alt="ElasticBreath" src="docs/screenshots/icon.png" />
</p>

<h1 align="center">弹性呼吸 (ElasticBreath)</h1>

<p align="center">
  用边缘视觉与弹性时间窗口提供"不可习惯化"的休息引导，绝不强制打断关键操作
</p>

<p align="center">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-blue.svg" />
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows-lightgrey.svg" />
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8-blue.svg" />
  <img alt="Language" src="https://img.shields.io/badge/language-C%23%20%2F%20WPF-blue.svg" />
</p>

---

**弹性呼吸（ElasticBreath）** 摒弃传统"定时强硬停止"设计，采用**渐进预警设计**：

工作时长分为安全 / 预警 / 硬性三档，休息分为基础 / 弹性 / 超时三档，视觉强度随档位渐进。

自动状态切换先调度倒计时、条件不满足自动取消，绝不强制打断关键操作

核心原则是**永不阻断中心视野**——无模态弹窗、不抢焦点、不黑屏。

<img width="932" height="766" alt="超时" src="https://github.com/user-attachments/assets/5d92b5b3-7a6f-4ef9-aad1-f0befde088df" />
<img width="1920" height="1080" alt="state-hard--ggg2" src="https://github.com/user-attachments/assets/5d1eceef-b2ff-4eed-a9ac-35618f2b00c9" />
<p align="center">
 仅实际效果演示，感谢鹰角网络的剧情图片，侵删
</p>

## 核心特性

- **渐进预警设计**：摒弃传统"定时强硬停止"设计，工作时长分为安全 / 预警 / 硬性三档，休息分为基础 / 弹性 / 超时三档，视觉强度随档位渐进，而非到点打断。
- **弹性时间窗口**：自动状态切换不是瞬时执行，而是先调度一段待处理倒计时；每秒复检触发条件，条件不满足则自动取消；用户也可点击通知卡片主动取消，避免在关键操作时被强制打断。
- **双向智能感知**：基于系统空闲时长与密集输入持续时长，自动识别用户离开与归来，无需手动启停。
- **左上角悬停触发**：将鼠标悬停在屏幕左上角达阈值即可静默切换工作 / 休息状态，无需点击
- **会话锁屏处理**：锁屏时强制进入空闲态、清零周期计时并清除待处理切换
- **无效休息不重置工时**：休息时长不足"最小有效休息"阈值时，切回工作会把"休息前工时 + 本次休息时长"作为新工时，防止随手切换就清零工时。
- **边缘呼吸光晕**：视觉效果不阻挡中心视野
- **便携免安装 / 不写注册表**：单文件便携版，解压即用，卸载只需删文件夹。


## 截图

### 三种状态

| 状态 | 说明 | 截图 |
|---|---|---|
| Warning | 工作预警：橙色边缘光晕，3 秒慢脉冲 | ![Warning](docs/screenshots/state-warning.png) |
| Hard | 工作硬性：红色边缘光晕，1 秒快脉冲 | ![Hard](docs/screenshots/state-hard.png) |
| RestBase | 休息基础：绿色边缘光晕 | ![RestBase](docs/screenshots/state-rest-base.png) |

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

从 Releases 页面下载便携版 zip，解压后直接运行 `ElasticBreath.exe`，无需安装。

## 配置说明

配置文件位于 `config/settings.json`，所有时长字段均以可读的算术表达式存储，例如 `"35*60"` 表示 35 分钟（2100 秒），保持配置文件的人类可读性，便于审阅与手动调整。

常用配置项也可通过应用内的设置界面（`SettingsWindow`）修改，保存时会自动写回 `config/settings.json`。

## 贡献

本项目由 [@Stargazed-Dreamer](https://github.com/Stargazed-Dreamer) 独立开发与维护，欢迎参与贡献。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

Copyright (c) 2026 Stargazed-Dreamer
