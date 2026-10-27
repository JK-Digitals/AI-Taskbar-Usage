# AI Taskbar Usage

Native Windows 11 x64 taskbar monitor for the usage windows exposed to Claude Code and OpenAI Codex CLI.

## What is implemented

- A borderless, non-activating native layered widget embedded as a child of the selected Windows taskbar. Its visual is rendered off-screen by WPF and transferred atomically to the native taskbar child. Detailed mode automatically falls back to compact mode when only a smaller strip is available.
- Show Claude Code only, OpenAI Codex only, or both side by side. The widget automatically becomes narrower when a single AI is selected. Detailed mode shows 5h and 7d; compact mode shows 5h / 7d.
- Cached values remain visible with a * stale marker and tooltip status. Login, offline, rate-limit, parser and endpoint errors are shown independently for each service.
- Left-click opens a detail window with both quota windows, reset timestamps, last refresh and the sanitized error state for the selected AIs. Right-click and the tray menu provide AI selection, refresh, provider pages, layout, refresh interval, opt-in auto-start, sanitized logs and exit.
- The Settings window provides a dedicated AI selector plus detailed/compact layout, system/dark/light theme, a primary or specific display (with all displays available only as an explicit option), left/center/right/before-notification positioning, a signed offset and optional fullscreen hiding.
- Refreshes run concurrently every 180 seconds by default, use a 15-second request timeout, cap response size, cache only quota snapshots, and back off after network/provider failures. Claude usage polling keeps an additional 180-second minimum to avoid Anthropic's aggressive OAuth usage throttle.
- Windows taskbar placement and z-order are re-evaluated every 200 milliseconds without re-rendering unchanged content. Explorer restarts, primary/secondary taskbar recreation, monitor/DPI changes, Start-menu transitions and auto-hide are handled by recreating or repositioning the widget. Short Shell/fullscreen transitions are debounced. The default is exactly one widget on the primary display.
- Credential discovery reads only the existing local Claude Code credential file (plus optional WSL distributions) and %CODEX_HOME%\\auth.json or the default Codex location. Tokens are never persisted, logged or displayed.
- The HTTP layer allows only TLS requests to api.anthropic.com and chatgpt.com. It does not invoke claude, codex, prompts, model calls or token-consuming APIs.

## Important Windows 11 taskbar caveat

Windows 11 removed the classic supported third-party inline taskbar toolbar API. This project does not patch Explorer or inject code into its process. It reparents its own non-activating layered window to the selected `Shell_TrayWnd` or `Shell_SecondaryTrayWnd`, making the widget a taskbar child instead of a competing topmost popup. The recommended position is immediately before the notification area; left, center, right and a signed offset are available for users who intentionally want a different placement.

The exact child-window layout is a Windows implementation detail and can change with future Windows builds. Real acceptance still requires a Windows 11 x64 machine with Explorer, taskbar alignment, DPI, auto-hide, monitor and fullscreen checks.

The taskbar-child approach was adapted from [Ch1nzo/claude-codex-usage-monitor](https://github.com/Ch1nzo/claude-codex-usage-monitor), which is distributed under the MIT License and credits Craig Constable's original Claude Code Usage Monitor.

## Provider notes

The Claude Code /api/oauth/usage and ChatGPT/Codex backend-api/wham/usage endpoints are internal/undocumented surfaces used by current CLI/web clients, not stable public APIs. They are isolated behind provider adapters and flexible parsing. Claude requests use the detected Claude Code version in the User-Agent and the app understands the numeric epoch expiry used by current Windows credentials. A 401/403 is displayed as re-login; an unrecognized response is displayed as unavailable rather than guessed.

References used for the adapters:

- [Anthropic Claude Code releases](https://github.com/anthropics/claude-code/releases)
- [OpenAI Codex authentication](https://github.com/openai/codex/blob/main/docs/authentication.md)
- [OpenAI Codex repository](https://github.com/openai/codex)

## Prerequisites

- Windows 11 x64.
- .NET 8 SDK for development and publishing. The runtime is not required for the self-contained release command below.
- Claude Code or Codex CLI signed in normally at least once. The app does not provide or automate login.

The self-contained Windows release is built with the installed .NET SDK; the test project still requires a reachable NuGet source for its xUnit packages.

## Development

From this directory:

    dotnet restore .\AI.TaskbarUsage.sln
    dotnet build .\AI.TaskbarUsage.sln -c Release -p:Platform=x64
    dotnet run --project .\src\AI.TaskbarUsage\AI.TaskbarUsage.csproj -c Release -r win-x64

Run tests:

    dotnet test .\tests\AI.TaskbarUsage.Tests\AI.TaskbarUsage.Tests.csproj -c Release -p:Platform=x64

## Open source and repository layout

AI Taskbar Usage is released under the [MIT License](LICENSE). This repository contains the complete application source, tests, contribution and security guidance, third-party attribution, and a Windows GitHub Actions workflow that restores, builds and tests the solution on `windows-latest`.

- `src/` contains the WPF application.
- `tests/` contains the parser and snapshot tests.
- `.github/workflows/build.yml` defines the public Windows build and test workflow.
- `AI Taskbar Usage.exe` is the self-contained Windows x64 application package, stored with Git LFS.

The packaged EXE contains no local settings, quota cache, logs, credentials or PDB/debug-symbol file. Development outputs and local data (`bin`, `obj`, `artifacts`, logs, settings, caches and credential files) are ignored and must not be committed.

To clone the packaged EXE as well as the source, install [Git LFS](https://git-lfs.com/) before cloning:

    git lfs install
    git clone https://github.com/JK-Digitals/AI-Taskbar-Usage.git

## Installation

AI Taskbar Usage is a portable Windows application: it does not install a service, require administrator rights or modify Windows system files.

1. Download `AI Taskbar Usage.exe` only from this GitHub repository.
2. Verify the download with the value in [SHA256SUMS.txt](SHA256SUMS.txt):

       Get-FileHash ".\AI Taskbar Usage.exe" -Algorithm SHA256

3. Start `AI Taskbar Usage.exe` and sign in to Claude Code and/or Codex CLI normally if you want to display their usage windows.

Windows SmartScreen can display an "unknown publisher" warning for a newly published EXE that is not yet code-signed. This is a reputation warning, not proof that the app is malicious. Do not bypass a warning blindly: verify the checksum, inspect or build the published source yourself, and download only from this repository. Code signing is the appropriate next step for stronger Windows publisher trust.

## Windows x64 release

    dotnet publish .\src\AI.TaskbarUsage\AI.TaskbarUsage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\artifacts\win-x64

The resulting AI Taskbar Usage.exe is the release artifact. No installer, updater, elevation or scheduled task is required. Auto-start is a reversible per-user HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run value.

## Local data and security model

Settings and quota cache are stored under %LOCALAPPDATA%\\AI Taskbar Usage. Tokens are not stored there. Logs contain provider names, HTTP status categories and exception type names only; secret-like fields are redacted before writing. The app uses the current user's permissions and does not require administrator rights.

OPENAI_API_KEY alone is intentionally not treated as a Codex plan session: it can authorize API usage, but it does not expose the ChatGPT/Codex quota window. Claude API keys are likewise not substituted for Claude Code subscription OAuth.

## Smoke-test checklist on a real Windows 11 PC

1. Sign in normally to Claude Code and Codex CLI, start the EXE, and confirm both values are readable directly on the taskbar.
2. Confirm one provider can fail independently without clearing the other provider's values.
3. Verify 401/403 shows a re-login state and no token appears in the widget, tooltip or log.
4. Switch centered/left taskbar alignment if available, then test compact mode and insufficient-space behavior.
5. Select each connected display in Settings, then restart Explorer, change display resolution/DPI, attach/detach a monitor, enable auto-hide, sleep/resume, and verify that only the selected taskbar contains the widget. Test "All displays" separately if desired.
6. Open a real fullscreen application and verify the widget hides without covering it or stealing focus.
7. Left-click for reset timestamps and refresh status; use the context menu to change interval, layout, auto-start and exit.
8. Switch between Claude only, Codex only and both; verify that the widget, tooltip, details window and polling follow the selection.

## Known limitations

- Anthropic and ChatGPT usage endpoints are undocumented and may change or deny access. The app reports this honestly and never fabricates a percentage.
- Windows taskbar geometry is not a stable public extension point. Explorer may destroy third-party child windows during a Shell transition; the app recreates its widget automatically when the taskbar is available again.
- WSL credential discovery is best effort and reads mounted \\\\wsl$ credential files when the WSL distribution exposes them to the Windows user.
- This build was runtime-checked on Windows 11 with live Explorer, display scaling, Windows-key transitions, left/right click and real Claude/Codex values. Other Windows builds, taskbar replacements and display arrangements still require their own acceptance check.
