# Installer

Builds a single `vatgram-setup.exe` that:

1. Detects the user's vPilot install folder (registry + common paths) — falls back to a folder picker.
2. Refuses to proceed if vPilot is currently running.
3. Installs the tray app to `%LOCALAPPDATA%\Programs\vatgram\` (per-user, no UAC).
4. Drops the plugin DLLs into `<vPilot>\Plugins\`.
5. Optional: desktop shortcut + Windows-startup checkbox.
6. Registers in Add/Remove Programs.
7. Auto-launches vatgram after install.

The tray app is published **self-contained** (~80 MB) so users don't need to install .NET 10 separately.

## Prerequisites

- **.NET 10 SDK** (already installed for development)
- **Inno Setup 6** — https://jrsoftware.org/isdl.php (one-click install, no config)

## Build

```powershell
.\installer\build.ps1
```

Output: `installer\setup\vatgram-setup.exe`

## Uninstaller behaviour

- Removes the tray app from `%LOCALAPPDATA%\Programs\vatgram\`
- Removes the plugin DLLs from `<vPilot>\Plugins\`
- Removes Start Menu / Desktop / Startup shortcuts
- **Keeps** user settings at `%APPDATA%\vatgram\` (token, chat ID, preferences) — user can delete manually if desired

## Notes for distribution

- **Code signing**: not configured. Users will see "Unknown Publisher" SmartScreen warning. Get a code-signing cert (~$100/yr Sectigo) and add `SignTool=` directive in the `.iss` to fix.
- **Auto-update**: not implemented. Add Velopack to the tray app for silent in-app updates.
- **vPilot SDK redistribution**: before public release, email Ross Carlson for permission to redistribute a plugin that links against `RossCarlson.Vatsim.Vpilot.Plugins.dll`.
