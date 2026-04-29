# ElasticBreath Implementation Route

## Selected Route
- Language: C#
- UI framework: WPF (.NET 8)
- Platform API: Win32 API via P/Invoke
- Build environment: Visual Studio 2022 (or `dotnet build`)

## Implemented in This Iteration
- State machine improvements:
  - Added idle auto-rest transition in working state: no input for `AutoRestAfterIdleSeconds` (default 30s) -> `Resting`
  - Preserved existing pause flow and return countdown flow
  - Added session lock/unlock handling: lock forces `Paused`; unlock resumes via return detection logic
- Corner trigger fix:
  - After one corner-triggered transition, cursor must leave all corners before another trigger can occur
- Settings page split:
  - Added a dedicated `SettingsWindow` (no longer embedded in main window)
  - Covers time, interaction, visual, display, audio, language and monitor selection
- Overtime glow intrusion growth:
  - Added dynamic glow thickness growth during rest overtime
  - Thickness increases progressively until configured `GlowMaxThicknessPixels`
- Help page:
  - Added dedicated `HelpWindow` with feature explanation text
- i18n:
  - Added JSON-based runtime localization loader
  - Added `zh-CN` and `en-US` language packs
  - Main window, settings window, help window, tray menu, and notification texts are loaded from i18n resources
- Multi-monitor fixes:
  - Switched overlay and notification positioning to Win32 pixel-based window placement to avoid DPI offset issues
  - Added secondary-monitor flash strategy service, controlled by setting toggle

## Design.md Gap Review (Current Status)
- Implemented now:
  - Dedicated settings page
  - Secondary monitor flashing toggle and behavior
  - SessionSwitch lock/unlock handling
  - 30s no-input auto-rest
  - Corner re-trigger gating
  - Runtime bilingual i18n
- Still pending (not yet implemented):
  - Corner trigger mini countdown ring visual
  - Fullscreen fallback: taskbar flashing + interval beep policy orchestration
  - Postpone quota reset by “complete rest” accounting rule
  - VM/RDP-specific input source filtering
  - Crash log write/recovery prompt flow
  - Game Bar / vendor overlay integration path

## Project Structure
- `ElasticBreath.App/Domain`: state and settings models
- `ElasticBreath.App/Services`: engine, input, localization, display, session, secondary flash
- `ElasticBreath.App/UI`: overlays, notification, settings, help windows
- `ElasticBreath.App/MainWindow.*`: control tower and orchestration
- `ElasticBreath.App/i18n`: localized UI text (`zh-CN.json`, `en-US.json`)

## Build
```powershell
dotnet build ElasticBreath.sln
```
