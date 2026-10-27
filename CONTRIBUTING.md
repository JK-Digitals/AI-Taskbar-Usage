# Contributing

Thanks for helping improve AI Taskbar Usage.

## Development setup

1. Use Windows 11 x64 and install the .NET 8 SDK.
2. Run `dotnet restore .\AI.TaskbarUsage.sln`.
3. Run `dotnet build .\AI.TaskbarUsage.sln -c Release -p:Platform=x64`.
4. Run `dotnet test .\tests\AI.TaskbarUsage.Tests\AI.TaskbarUsage.Tests.csproj -c Release -p:Platform=x64`.

## Pull requests

- Keep changes focused and describe the Windows version, monitor layout and DPI used for manual testing.
- Never commit OAuth tokens, credential files, logs, settings, caches or provider responses containing account data.
- Preserve the non-activating taskbar-child behavior and test Explorer restart, the Windows key, taskbar clicks and multi-monitor selection when changing window placement code.
- Add or update focused tests for parsers, settings migrations and other deterministic logic.

By contributing, you agree that your contribution is licensed under this repository's MIT License.
