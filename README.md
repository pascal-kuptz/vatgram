# vatGram

**Use your preferred second device — phone or tablet — to read and answer vPilot messages while you're in the cockpit.** vatGram bridges vPilot ↔ Telegram so you can keep the chat on your iPad next to the yoke instead of alt-tabbing out of the sim. Bonus: tune COM1/COM2 and your transponder code from the same chat via SimConnect.

> 🛂 **vatGram does not enable flying AFK.** Per VATSIM Code of Conduct you must remain at the controls of your aircraft and respond promptly. vatGram exists to make that *easier* by letting you use a comfier input device — never as a remote-control or auto-pilot for being away from your station.

## Install

Download `vatgram-setup.exe` from the [installer](installer/setup/) folder, run it. The installer:

1. Detects your vPilot install path (registry + common paths) — falls back to a folder picker.
2. Installs the tray app to `%LOCALAPPDATA%\Programs\vatGram\` (per-user, no UAC).
3. Drops the plugin DLLs into `<vPilot>\Plugins\`.
4. Optional: desktop shortcut + Windows-startup checkbox.

The tray app ships with a self-contained .NET 10 runtime — no separate dependencies.

## First-run setup

The onboarding wizard walks you through:

1. Creating your own Telegram bot via @BotFather (60 seconds).
2. Pasting the bot token into vatGram.
3. Sending `/start` to your bot — vatGram binds the chat automatically.

Then start vPilot. The plugin auto-connects.

## Telegram commands

Inspired by [xPilot dot commands](https://docs.xpilot-project.org/docs/client/dot-commands). Both `/cmd` and `.cmd` work — Telegram shows the slash variants in its `/` autocomplete menu.

**Reply**
- *plain text* — replies to last sender
- `/chat CALLSIGN message` (alias `/msg`, `/to`) — send PM to specific callsign
- `/radio message` — transmit text on current TX frequency

**Info (vPilot)**
- `/metar STATION` (alias `/wx`) — request METAR
- `/atis CALLSIGN` — request controller ATIS

**Aircraft (vPilot)**
- `/ident` — squawk ident
- `/modec on|off` — toggle mode C
- `/dis` — disconnect from VATSIM

**Cockpit (MSFS via SimConnect — requires MSFS running)**
- `/com1 118.700` / `/com2 121.500` — tune COM radios
- `/squawk 1234` (alias `/x 1234`) — set transponder code

**Filtering**
- `/ignore CALLSIGN` / `/unignore CALLSIGN` / `/ignorelist`

**Help**
- `/help` — list commands

## Settings storage

`%APPDATA%\vatgram\settings.json`

## Build from source

Requires .NET 10 SDK. Set `VPilotPath` in `Directory.Build.props` if your vPilot install is elsewhere.

```
dotnet build Vatgram.sln -c Release
```

To build the installer (requires [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```
powershell -ExecutionPolicy Bypass -File installer/build.ps1
```

Output: `installer/setup/vatgram-setup.exe`

## Project layout

```
src/
  Vatgram.Shared/   netstandard2.0 — IPC message contracts
  Vatgram.Plugin/   net472         — vPilot plugin (IPlugin)
  Vatgram.Tray/     net10-windows  — WPF tray app, Telegram bot, SimConnect
installer/          — Inno Setup script + build pipeline
```

Plugin and tray app communicate over named pipe `Vatgram.Bridge.v1` with length-prefixed JSON. Cockpit commands (COM/squawk) talk to MSFS directly via SimConnect from the tray process.
