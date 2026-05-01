# Releases

Reverse-chronological release log for vatGram. New entries go on top — every shipped version gets one.

---

## v1.0.0 — Initial release ✈️

📅 2026-04-29

**Use your preferred second device — phone or tablet — to read and answer vPilot messages while you're in the cockpit.** vatGram bridges vPilot ↔ Telegram so you can keep the chat on your iPad next to the yoke instead of alt-tabbing out of the sim. Bonus: tune COM1/COM2 and your transponder code from the same chat via SimConnect.

> 🛂 **Not for flying AFK.** Per VATSIM Code of Conduct you must remain at the controls of your aircraft and respond promptly. vatGram exists to make that *easier* by letting you use a comfier input device — never as a remote-control or auto-pilot for being away from your station.

### ✨ Highlights

- 🔔 **Real-time push notifications** to Telegram for private messages, radio chatter on your tuned freqs, broadcasts, SELCAL, and connect/disconnect events.
- 💬 **Reply from Telegram** — just type, it goes back as a vPilot PM. Or `/chat CALLSIGN message` for a different recipient.
- 📋 **METAR & ATIS on demand** — `/metar EDDF`, `/atis EDDF_TWR`.
- 🎛️ **Cockpit control from your phone** — `/com1 118.700`, `/com2 121.500`, `/squawk 1234`, `/ident`.
- 🌙 **Quiet hours** with 30-min slots.
- 🔇 **Per-callsign mute list** — `/ignore`, `/unignore`, `/ignorelist`.
- ⚡ **Bring-your-own bot** — your data stays between you, Telegram, and your machine. No third-party server.

### 📥 Install

1. Download **`vatGram-Setup.zip`** from the [release page]().
2. Extract → run **`vatgram-setup.exe`**.
3. *(Windows SmartScreen will warn — "More info" → "Run anyway". One-time, expected for unsigned installers.)*
4. Wizard auto-detects your vPilot install, drops the plugin in, sets up the tray app.
5. First launch walks you through creating a Telegram bot via @BotFather (60 seconds) and binding your chat with `/start`.

### ✅ Requirements

- **Windows 10 1809+** or Windows 11 (Mica backdrop on Win11).
- **vPilot** installed (auto-detected by the wizard).
- **MSFS** running (only needed for COM/squawk commands; everything else works with just vPilot).
- A Telegram account.

### 🧭 Telegram commands

Type `/` in your bot chat for autocomplete, or use the shorter xPilot-style dot prefix:

```
/metar STATION       /atis CALLSIGN
/chat CALLSIGN msg   /radio msg
/com1 118.700        /com2 121.500
/squawk 1234         /ident
/modec on|off        /dis
/ignore CALLSIGN     /unignore CALLSIGN     /ignorelist
/help
```

Plain text in the chat = reply to the last sender. Done.

### 🐛 Known issues

- **SmartScreen warning** on first run — no code-signing certificate yet. Click "More info" → "Run anyway".
- **Bot menu** in Telegram may take a few seconds to refresh after first install.
- **No auto-updater** — for now, watch this page for updates and reinstall manually.

### 🙏 Credits

- vPilot plugin SDK by **Ross Carlson** — vatGram talks to vPilot through his official Plugin API.
- VATSIM Network for the simming community this was built for.
- Built with .NET 10 (WPF) + `Telegram.Bot` + `SimConnect.NET` + `H.NotifyIcon.Wpf`.

### ⚠️ VATSIM Code of Conduct

vatGram is a **second-screen UX tool**, not an AFK-enabler. By using it you confirm you will:

- Remain at the controls of your aircraft for the entire flight.
- Respond promptly to ATC and supervisors as if you were typing in vPilot directly.
- Never use vatGram to simulate presence while away from your station.

If you can't satisfy these — disconnect from the network. The risk is yours.

---

Made with ♥ by [flywithpascal.com](https://flywithpascal.com) in Switzerland 🇨🇭
