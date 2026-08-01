<p align="center">
  <img src="https://img.shields.io/badge/version-1.1.0-blue?style=for-the-badge" alt="Version">
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Android-0078d4?style=for-the-badge" alt="Platform">
  <img src="https://img.shields.io/badge/license-MIT-green?style=for-the-badge" alt="License">
  <img src="https://img.shields.io/badge/.NET-8-purple?style=for-the-badge&logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/Kotlin-1.9-orange?style=for-the-badge&logo=kotlin" alt="Kotlin">
  <img src="https://img.shields.io/badge/Compose-Material3-blue?style=for-the-badge" alt="Compose">
</p>

<h1 align="center">GamePadEcosystem</h1>

<p align="center">
  <b>Offline Multiplayer Virtual Gamepad</b><br>
  Turn your Android phones into wireless Xbox 360 controllers for your Windows PC<br>
  Zero cloud, zero accounts, sub-5ms latency over WiFi hotspot
</p>

<p align="center">
  <a href="https://github.com/yaleedhaque/GamePadEcosystem/releases/latest">
    <img src="https://img.shields.io/badge/download-Server.exe-blue?style=for-the-badge&logo=windows" alt="Download Server">
  </a>
  <a href="https://github.com/yaleedhaque/GamePadEcosystem/releases/latest">
    <img src="https://img.shields.io/badge/download-APK-green?style=for-the-badge&logo=android" alt="Download APK">
  </a>
</p>

---

## What is GamePadEcosystem?

Turn your Android phones into **wireless Xbox 360 controllers** for your Windows PC. No cables, no Bluetooth adapters, no cloud accounts, no subscriptions. Just your phone's WiFi hotspot and this app.

```
┌──────────────────┐      UDP (sub-5ms)      ┌──────────────────────┐
│   Android Phone   │ ◄───────────────────►  │   Windows 11 PC      │
│   GamePad Client  │      WiFi Hotspot      │   GamePad Server     │
│   Kotlin/Compose  │                        │   C# · .NET 8        │
└──────────────────┘                        └──────────┬───────────┘
        ▲                                               │
        │          UDP Broadcast                         │
        │          Auto-Discovery                        ▼
        │                                       ┌───────────────┐
        └──── Multiple phones ─────────────────►│  ViGEmBus     │
                                                │  Xbox 360 x N  │
                                                └───────┬───────┘
                                                        │
                                                        ▼
                                                ┌───────────────┐
                                                │   Emulators   │
                                                │ RetroArch     │
                                                │ PCSX2 · Dolphin│
                                                │ RPCS3 · Yuzu  │
                                                └───────────────┘
```

---

## Download

### Windows Server

1. Go to **[Releases](https://github.com/yaleedhaque/GamePadEcosystem/releases/latest)**
2. Download **`GamePadServer-v1.1.0-win-x64.zip`**
3. Extract anywhere
4. Run **`StartServer.bat`** (or `GamePadServer.exe`) as Administrator

### Android Client

1. Go to **[Releases](https://github.com/yaleedhaque/GamePadEcosystem/releases/latest)**
2. Download **`GamePadController-v1.1.0.apk`**
3. Transfer to your Android phone
4. Install (enable "Install from unknown sources" if prompted)

---

## Quick Start

1. **Run GamePadServer.exe** on the PC (as Administrator)
2. The server **automatically starts a WiFi hotspot** (`GamePad_Server` / `gamepad123`)
   - If the PC has internet, it uses the native Windows hotspot
   - If the PC is offline, it force-enables the hotspot using a virtual loopback adapter (no internet required)
3. **Connect each phone** to the `GamePad_Server` WiFi network
4. **Install & open** the GamePad Controller app on each phone
5. The app **auto-discovers** the server — no IP entry needed
6. Each phone becomes a **virtual Xbox 360 controller**
7. Open any emulator — it detects the controllers natively

**That's it.** No IP addresses to type, no ports to configure. Fully offline — no router, no cloud.

---

## Features

| Feature | Details |
|---------|---------|
| **Multiplayer** | Up to 8 phones simultaneously |
| **Auto-Discovery** | No manual IP entry — UDP broadcast finds the server |
| **One-Click Hotspot** | Server auto-starts a WiFi hotspot; works even with no internet (loopback-adapter trick) |
| **Self-Repairing** | Watchdog restarts the hotspot if it drops; auto-shutdown disabled |
| **Sub-5ms Latency** | UDP raw sockets + binary 34-byte packets |
| **100% Emulator Compat** | ViGEmBus creates native Xbox 360 controllers |
| **Motion Controls** | Gyroscope + accelerometer for steering/aiming |
| **Multiple Profiles** | Xbox Standard, Retro D-Pad, FPS+Motion, Racing, Custom |
| **Layout Editor** | Drag-to-reposition buttons, resize, toggle visibility |
| **Haptic Feedback** | Button press vibration on every touch |
| **Zero Drift Protection** | Timeout disconnects zero all inputs automatically |

## What's New in v1.1.0

- **Hotspot actually starts.** Fixed a broken PowerShell script inside the server that prevented the Windows hotspot from launching (a `//` comment was being executed as code).
- **Works offline.** New virtual loopback adapter lets the hotspot start even when the PC has no internet connection at all.
- **No more discovery spam.** The server no longer mistakes its own broadcasts for new devices — the previous infinite loop that flooded `server.log` with phantom "Android Device" entries is fixed.
- **Stable logs.** Logs are capped at 1MB with automatic rotation.

---

## Screenshots

<!-- Add screenshots here -->
<!-- ![Connection Screen](docs/screenshots/connection.png) -->
<!-- ![Controller Layout](docs/screenshots/controller.png) -->
<!-- ![Server HUD](docs/screenshots/server-hud.png) -->

*Contributions welcome — add screenshots of your setup!*

---

## Compatible Emulators

Any software that reads XInput / Xbox 360 controllers:

| Emulator | Status |
|----------|--------|
| RetroArch | Fully compatible |
| PCSX2 | Fully compatible |
| Dolphin | Fully compatible |
| RPCS3 | Fully compatible |
| Yuzu / Ryujinx | Fully compatible |
| Cemu | Fully compatible |
| PPSSPP | Fully compatible |
| ANY XInput game | Fully compatible |

---

## Prerequisites

### Windows PC (Server)

| Tool | Version | Download |
|------|---------|----------|
| ViGEmBus Driver | 1.17+ | [github.com/nefarius/ViGEmBus](https://github.com/nefarius/ViGEmBus/releases) |
| .NET 8 Runtime | 8.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) (only if building from source) |

### Android Phone (Client)

| Tool | Version | Download |
|------|---------|----------|
| Android | 8.0+ (API 26) | Pre-installed on most phones |
| APK | Latest | [GitHub Releases](https://github.com/yaleedhaque/GamePadEcosystem/releases/latest) |

---

## Build from Source

### Windows Server

```bash
# Clone
git clone https://github.com/yaleedhaque/GamePadEcosystem.git
cd GamePadEcosystem

# Build
cd server\src\GamePadServer
dotnet build -c Release

# Run
dotnet run

# Publish release (framework-dependent, small footprint)
dotnet publish -c Release -o ./publish
```

Or open `GamePadEcosystem.sln` in Visual Studio 2022 and press F5.

### Android Client

```bash
cd client

# Build release APK (signed with debug key, installable on any phone)
./gradlew assembleRelease

# APK output
# app/build/outputs/apk/release/app-release.apk

# Install on connected phone
adb install app/build/outputs/apk/release/app-release.apk
```

Or open `client/` folder in Android Studio → Build → Build APK.

---

## Protocol Summary

- **Transport:** UDP (input + discovery)
- **Packet Size:** 34 bytes (binary, little-endian)
- **Update Rate:** ~125Hz (8ms intervals)
- **Discovery:** UDP broadcast on port 9878
- **Max Players:** 8 simultaneous controllers

See [`shared/protocol/PROTOCOL.md`](shared/protocol/PROTOCOL.md) for the complete wire format specification.

---

## Architecture

```
GamePadEcosystem/
├── server/src/GamePadServer/     Windows Server (.NET 8)
│   ├── Core/Protocol.cs          Binary packet format
│   ├── Core/ClientManager.cs     Multi-device slot management
│   ├── Network/DiscoveryService  UDP auto-discovery (self-broadcast safe)
│   ├── Network/InputListener     High-perf UDP receiver
│   ├── Network/HotspotManager    4-strategy hotspot detection
│   ├── Network/LoopbackAdapter   Offline hotspot support (virtual NIC)
│   ├── VirtualController/        ViGEmBus Xbox 360 mapping
│   └── UI/ServerHud              Real-time console display
│
├── client/app/src/               Android Client (Kotlin/Compose)
│   ├── network/                  Protocol + UDP client + discovery
│   ├── sensors/                  Gyro + accelerometer input
│   ├── profiles/                 Controller layout presets
│   └── ui/                       Full controller + layout editor
│
└── shared/protocol/              Wire format specification
```

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

1. Fork the repo
2. Create a feature branch
3. Make your changes
4. Submit a pull request

---

## License

[MIT License](LICENSE) — Copyright (c) 2026 Yaleed Haque

---

## Support

- Report bugs: [GitHub Issues](https://github.com/yaleedhaque/GamePadEcosystem/issues)
- Request features: [GitHub Issues](https://github.com/yaleedhaque/GamePadEcosystem/issues)
- Star this repo if you find it useful
