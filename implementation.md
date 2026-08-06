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
  - Added session lock/unlock handling: lock forces `Idle` (resets `WorkingCycleElapsed`/`RestingCycleElapsed`/idle activity probe, marks rest effective, clears pending transitions); unlock keeps current state and only re-publishes snapshot
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

## Implemented in Later Iterations
- Postpone feature (`PostponeService`): cooldown (`PostponeCooldownSeconds`, default 5 min) + daily limit (`DailyPostponeLimit`, default 3) + complete-rest quota reset; exposed via the main window "推迟" button and `BreathEngine.TryPostpone()`.
- Sound reminders (`SoundService`): zero-dependency in-memory PCM WAV generation with volume baked into samples; transition/warning/hard/overtime tones plus fullscreen-fallback beep; controlled by `EnableSound`/`ReminderVolumePercent`/`EnableFullscreenFallbackBeep`.
- Crash recovery (`CrashRecoveryService` + `CrashRecoveryWindow`, fed by `CrashLogger`): crash logs written under `%TEMP%\ElasticBreath\crash\`; recovery prompt with copy/dismiss shown on next launch.
- Corner trigger mini countdown ring (`CornerRingWindow`): arc fills with hover progress at the active screen corner (design §5.3).
- Fullscreen fallback (`FullscreenFallbackOrchestrator`): taskbar flashing (FlashWindowEx) + 5 s interval beep when the overlay is hidden by a fullscreen app and an active reminder exists (design §7).
- Remote control foreground filtering (`RemoteInputFilterService`): detects ToDesk/RayLink/SunLogin/TeamViewer/AnyDesk/RustDesk foreground processes.
- Unit tests project `ElasticBreath.Tests` (engine transitions, postpone service, corner trigger, expression evaluator, settings sanitize).

## Design.md Gap Review (Current Status)
- Implemented now:
  - Dedicated settings page
  - Secondary monitor flashing toggle and behavior
  - SessionSwitch lock/unlock handling
  - 30s no-input auto-rest
  - Corner re-trigger gating
  - Runtime bilingual i18n
  - Corner trigger mini countdown ring visual
  - Fullscreen fallback: taskbar flashing + interval beep policy orchestration
  - Postpone feature: cooldown (`PostponeCooldownSeconds`) + daily limit (`DailyPostponeLimit`) + complete-rest quota reset
  - Crash log write/recovery prompt flow
  - Sound reminders (zero-dependency in-memory PCM WAV, volume-controlled)
  - Remote control foreground filtering (ToDesk/RayLink/TeamViewer/AnyDesk/RustDesk/SunLogin) — partial
- Still pending (not yet implemented):
  - VM/RDP-specific input source filtering (current `RemoteInputFilterService` matches remote-control process names only, not RDP/VM session input sources)
  - Game Bar / vendor overlay integration path

## Project Structure
- `ElasticBreath.App/Domain`: state and settings models
- `ElasticBreath.App/Services`: engine, input, localization, display, session, secondary flash, postpone, sound, crash recovery, fullscreen fallback
- `ElasticBreath.App/UI`: overlays, notification, settings, help, corner ring, crash recovery windows
- `ElasticBreath.App/MainWindow.*`: control tower and orchestration
- `ElasticBreath.App/i18n`: localized UI text (`zh-CN.json`, `en-US.json`)
- `ElasticBreath.Tests`: unit tests (engine transitions, postpone service, corner trigger, expression evaluator, settings sanitize)

## Build
```powershell
dotnet build ElasticBreath.sln
```
