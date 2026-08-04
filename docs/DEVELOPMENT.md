# GamePadEcosystem — Development & History

Developer-facing companion to `README.md`. Covers architecture, wire protocol, internals, build/release process, known bugs, and the full release history. Maintained by the developer (`Md. Yaleed Haque`) as the source of truth for this repo; if you are here to fix or extend the project, read this first.

---

## Versions / Releases

| Version | Date | Scope | Tag | CI run | Notes |
|---|---|---|---|---|---|
| 1.1.0 | 2026-08-01 | server + client | (deleted) | — | Dirty-tree commit `146ab46`, first republished server |
| 1.1.1 | 2026-08-01 | server + client | (deleted) | 30694210361 | APK + server zip released; CI gradlew/exec-bit fixes |
| 1.1.2 | 2026-08-02 | server + client | `v1.1.2` | 30743687237 | Input-chain fixes (8 root causes) |
| 1.1.3 | 2026-08-02 | server + client | `v1.1.3` | 30747856298 | Player-swap fix; sensorLandscape |
| 1.1.4 | 2026-08-02 | server only | `v1.1.4` | 30748613806 | Pads never auto-destroyed |

- v1.0.0 / v1.1.0 / v1.1.1 tags + releases were deleted from GitHub (2026-08-02); local tags may still exist.
- Client has not changed since v1.1.3 (versionCode 5); v1.1.4 re-released the identical APK under a new asset name.
- csproj `Version` stays `1.1.0`; patch bumps are tracked by tag, not csproj.

---

## Architecture

- **Server** — C# .NET 8, `net8.0-windows`, `UseWindowsForms=true`, admin-only manifest.
  - UDP input on **9876**, UDP discovery broadcast on **9878**. TCP 9877 is declared in the protocol but **unused** (do not introduce binding on it).
  - Virtual Xbox 360 pads via **ViGEmBus** (Nefarius.ViGEm.Client 1.21.256).
  - Hotspot auto-starts: SSID `GamePad_Server`, password `gamepad123`, gateway `192.168.137.1`.
  - Max **8** players. Framework-dependent build → needs .NET 8 Desktop Runtime.
  - Source: `server/src/GamePadServer/` (10 files).
- **Client** — Kotlin, Compose BOM 2024.12.01, AGP 8.7.0, Kotlin 2.1.0, Gradle 8.11.1, JDK 17, minSdk 26 / target+compile 35, `versionCode 5` / `versionName "1.1.3"`, package `com.gamepad.controller`.
  - Single `MainActivity`, no ViewModel/Nav; manual screen swap via `showGamepad` state.
  - **v1.1.3+**: `screenOrientation` = `sensorLandscape` (rotates both landscape sides, never portrait).
  - Sends 34-byte packets at ~125 Hz (delay 8 ms) + heartbeat every 1200 ms (server timeout 2000 ms).
  - Release signed with the **debug keystore** (R8 minified, ~1 MB).
  - Source: `client/app/src/main/java/com/gamepad/controller/` (12 Kotlin files + manifest + res).

### Server files
| File | Responsibility |
|---|---|
| `Program.cs` | Boot order: ClientManager → ControllerManager(ViGEmClient) → DiscoveryService → InputListener → ServerHud → HotspotManager.Start() BEFORE listeners (prints real IP). Watchdog 1000 ms (RemoveStaleClients + CheckDisconnections 2000 ms). `Q` to quit; KeyAvailable wrapped in try/catch (survives no-console launch). FileLog → `server.log` in AppContext.BaseDirectory, 1 MB rotation → `server.log.old`. `GetLocalIp` via socket.Connect("8.8.8.8",80)+LocalEndPoint. |
| `ClientManager.cs` | `_clients` (slot→ConnectedClient) + `_endpointToSlot` ConcurrentDictionaries; `AssignSlot` reuses freed slots; `RemoveStaleClients` only flips `IsConnected=false` (keeps maps for reconnect). **v1.1.2**: slot-reuse now `TryRemove`s the OLD endpoint's stale mapping so a returning zombie device can't hijack the new player's controller. |
| `DiscoveryService.cs` | One UdpClient bound Any:9878, EnableBroadcast. Listener replies to ANY packet with identity; broadcaster every 2 s to 255.255.255.255 + per-interface directed broadcasts. 3 self-spam guards (bug B fix): drop own IPs/broadcast src, drop packets starting `GPAD_SERVER_V1`, 10 s ip:port dedupe. `_seenDevices` never evicted (minor leak, acceptable). |
| `HotspotManager.cs` | 4-strategy cascade: EnsureWifiAdapterEnabled → LoopbackAdapter.EnsureCreated → TryTetheringApi (WinRT) → CheckHostedNetworkSupport + TryHostedNetwork (netsh wlan) → EnableMobileHotspot (ICS registry: ScopeAddress=192.168.137.1, pool .2-.254, `sc config SharedAccess` + `net start`) → UseExistingLan fallback. Tethering via PowerShell script to `%TEMP%\gamepad_tether.ps1`, WinRT NetworkOperatorTetheringManager, AsTask found by reflection, sentinels `HOTSPOT_ACTIVE`/`ERROR` polled 500 ms up to 45 s, script loops forever (keeps hotspot alive). `DisableAutoShutdown` = reg `...\icssvc\Settings PeerlessTimeoutEnabled=0` (defeats 5-min kill). KeepAlive task every 30 s re-checks adapter. `FindHotspotAdapter` assigns 192.168.137.1/24 if no IP. **Bug pattern**: async stdout — tether PS process never exits so `ReadToEnd()` blocks forever; MUST use `BeginOutputReadLine` + `OutputDataReceived` + poll. `RunNetsh`/`RunCmd` are short-lived, safe with `ReadToEnd`. |
| `LoopbackAdapter.cs` | SetupAPI P/Invoke creates Microsoft KM-TEST Loopback Adapter (ROOT\NET\0001..0999, hardware ID `*msloop`, netloop.inf quiet install), renames "GamePad Loopback", static IP 10.99.0.1/24. Gives Windows a connection profile to share so the hotspot works with zero physical NICs. Matches EXACT "Microsoft KM-TEST Loopback Adapter" (bug C — substring match hit "Loopback Pseudo-Interface 1"). |
| `InputListener.cs` | UdpClient Any:9876, 2 MB recv buffer, timeout 5000 ms. Single receive task, ALL processing synchronous on that thread. min 34 bytes → validate magic → assign slot on first contact + reply assignment → update LastSeen/LastSequence → update controller. **v1.1.2 fix**: after 2 s timeout the client was marked disconnected but kept its endpoint→slot map, so every subsequent packet was DROPPED forever (zombie). Now revives `IsConnected=true` on next packet; `UpdateController` auto-recreates the destroyed controller. `Dispose` hardened (try/catch around Cancel/Close/Wait/Dispose). |
| `ControllerManager.cs` | One ViGEmClient, `IXbox360Controller?[8]` + per-slot locks + `_active`/`_lastInput`/`_zeroed`. `CreateController` lazy on first input. `UpdateController`: buttons→`SetButtonState` (all 15), sticks→`SetAxisValue` (int16 passthrough), triggers→`SetSliderValue` (byte passthrough). No scaling anywhere. **v1.1.3**: `CheckDisconnections(2s)` only ZEROES + logs once (`_zeroed[]`), device kept alive (no Windows XInput re-enumeration → no game player remap). **v1.1.4**: `ReleaseStaleControllers` REMOVED — pads are NEVER destroyed until shutdown. 30 s idle cleanup removed (bathroom-break/screen-off penalty + residual remap risk). |
| `ServerHud.cs` | Box-drawing HUD, 500 ms redraw, Interlocked stats, skips if redirected/<60x12. Green dot <2 s alive, yellow otherwise. |

### Client files
| File | Responsibility |
|---|---|
| `MainActivity.kt` | God-object: owns UdpInputClient, DiscoveryClient, SensorInputManager, all state, heartbeat coroutine, send path. startDiscovery → onServerFound → connectToServer → stops discovery, starts UDP (connected=true immediately), starts sensors, heartbeat 1200 ms. `sendInputPacket` injects gyro deltas when showMotion. |
| `UdpInputClient.kt` | DatagramSocket reuseAddress, trafficClass 0x10 (IP_TOS LOW_DELAY), soTimeout 3000, bound to WiFi network via NetworkBinder. Response listener on raw Thread("UDP-Response-Listener"), detects assignment `0xAA` slot name → onPlayerAssigned. sendInput stamps AtomicInteger sequence masked 16 bits. |
| `DiscoveryClient.kt` | Phase1 direct probes to 6 known gateways (soTimeout 1500, listen 2000 ms), Phase2 broadcast sweep up to 10 rounds (1500 ms each). MulticastLock "GamePadDiscovery" held for whole discovery. computeBroadcast for subnet broadcasts. |
| `NetworkBinder.kt` | Binds DatagramSocket to hotspot Network via ConnectivityManager (priority: active WiFi → active non-cellular/non-VPN → any WiFi → fallback). Prevents UDP leaking over mobile data. |
| `ButtonLayout.kt` | `ButtonLayout(id,type,x,y fractions 0..1,sizeDp,label,color,visible,buttonFlag UInt)`. `ControllerLayout(name,buttons)`. 19 ElementTypes. LayoutManager = SharedPreferences `gamepad_layouts`, keys `current_layout` + `custom_<name>`, `commit()` sync writes (was `apply()`, data-loss fix). `defaultXboxLayout()` = 17 controls; **v1.1.2**: triggers moved to y=0.14 (was 0.09 — overlapped LB/RB touch zones). `ensureStickClicks` = v1.1.1 migration appending L3/R3. org.json serialization (no third-party lib). |
| `ControllerProfile.kt` | 5 profiles — XBOX_STANDARD (no motion), RETRO_DPAD (no sticks/triggers/bumpers), FPS_MOTION (no dpad, motion), RACING (no center, motion), CUSTOM (all + motion). showMotion gates gyro injection. |
| `SensorInputManager.kt` | gyro + accelerometer at SENSOR_DELAY_GAME. `getMotionAxis()` = accel tilt (DEAD CODE, never called). `getGyroDelta()` = raw gyro rad/s × sensitivity (default 1.0, no UI). HIGH_SAMPLING_RATE_SENSORS declared but unused. |
| `GamePadScreen.kt` | LaunchedEffect send loop 8 ms → onPacketReady. **v1.1.2 input fixes**: (a) sticks normalize against radius in PX (was dp — premature saturation ~3x), (b) stick Y NEGATED for XInput (screen Y grows down, XInput +Y=up; was inverted in-game), (c) triggers = hold-to-press absolute-x mapping, released to 0 (was px/dp + stale-baseline accumulation that stuck), (d) button press wrapped in try/finally so release always fires (was stuck-on on gesture cancel). Layout editor via bottom-right FAB. collapsible cyan #00E5FF toolbar, size slider 24-160 dp, visible switch, Reset/Save&CExit/Cancel. Edit mode: tap select (2 dp cyan border), detectDragGestures normalize by widthPixels, clamp 0.02-0.98. |
| `Components.kt` | Haptics, TouchButton, AnalogStick (deadzone 0.08), DPad, TriggerSlider. |
| `ConnectionScreen.kt` | connect/disconnect, discovery spinner, manual IP, profile pills, Quick Start card. |

Scaling: sticks ×32767, triggers ×255.

---

## Wire protocol (byte-for-byte contract)

> `Protocol.cs` ≡ `Protocol.kt` ≡ `shared/protocol/PROTOCOL.md`. Keep these three identical.

- Ports: **9876** input UDP, **9877** TCP reserved/unused, **9878** discovery UDP.
- **Discovery**: client sends `GPAD_DISCOVER_V1` + deviceName (broadcast + direct probes to 192.168.137.1, 192.168.43.1, 192.168.58.1, 192.168.1.1, 192.168.0.1, 10.0.0.1). Server replies / passively broadcasts every 2 s `GPAD_SERVER_V1|hostname`.
- **InputPacket** = 34 bytes little-endian: Magic int32 `0x47504144` ("GPAD"), PacketType byte (1=input, 2=heartbeat, 3=gyro declared-unused, 4=config), DeviceId byte (server-assigned slot; client sends 0), Sequence uint16, Buttons uint32 bitmask, LeftX/LeftY/RightX/RightY int16, LeftTrigger/RightTrigger byte(0-255), GyroX/GyroY/GyroZ float32 rad/s. Gyro floats ride inside every INPUT frame when `profile.showMotion` (client never sends a separate gyro packet).
- **Button flags**: A=0x01, B=0x02, X=0x04, Y=0x08, LB=0x10, RB=0x20, Back=0x40, Start=0x80, LStick=0x100, RStick=0x200, DUp=0x400, DDown=0x800, DLeft=0x1000, DRight=0x2000, Guide=0x4000.
- **Assignment** (server→client on first contact): `[0]=0xAA`, `[1]=slot 0-7`, `[2..]=UTF-8 hostname`.
- **Disconnect**: 2000 ms silence → server ZEROES controller (anti-drift, device KEPT ALIVE, HUD marks dead). v1.1.4: pads NEVER destroyed on idle — only at server shutdown (DestroyController via Dispose). XInput player indices can never change mid-session.

---

## Known bugs A–D (fixed, for reference)

- **Bug A**: embedded PS script had `//` C#-style comment → PowerShell crashed tethering → hotspot never started. Fixed → `#`.
- **Bug B**: discovery socket received own broadcasts → 40 MB log flood. Fixed: 3 self-guards + 10 s dedupe.
- **Bug C**: loopback substring match hit "Loopback Pseudo-Interface 1". Fixed: exact match.
- **Bug D**: no log rotation. Fixed: 1 MB cap → `server.log.old`.

---

## Build & release

- **Server**: `dotnet publish -c Release -o <dir>` (framework-dependent).
- **Client**: `./gradlew :app:assembleRelease` → `app/build/outputs/apk/release/app-release.apk` (~1 MB).
- **CI**: `.github/workflows/release.yml` — tag `v*` → build-server (windows-latest, dotnet 8.0.x) + build-android (ubuntu, temurin 17, android-actions/setup-android@v3, gradle setup-gradle@v4, `./gradlew :app:assembleRelease --no-daemon`) + release (softprops/action-gh-release@v2, generate_release_notes).
- **History**: (a) Unix client/gradlew not committed → regenerated via `gradlew.bat wrapper`; (b) no exec bit → Permission denied → `git update-index --chmod=+x`.

### Deployed artifacts (D:\release on dev machine)
- `GamePadServer.exe`/dll + deps + runtimeconfig (v1.1.4), `Nefarius.ViGEm.Client.dll`, `StartServer.bat`, `GamePadController-v1.1.3.apk` (1,069,628 B, versionCode 5, SHA256 6EB0A404…), `server.log`, `GamePadControllerDetails.txt` + `gamepadserverdetails.txt` (full technical reports, verbatim sources, hashes, bug history), src snapshot synced to HEAD.
- `StartServer.bat`: `powershell Start-Process '%~dp0GamePadServer.exe' -Verb RunAs`.

---

## Changelog

### v1.1.4 (2026-08-02) — pads never auto-destroyed
- Removed `ReleaseStaleControllers` (ControllerManager), `Protocol.ReleaseTimeoutMs`, and the watchdog call in Program.cs.
- Pads live until server shutdown; 2 s timeout zeroes inputs only.
- Verified: 40 s-idle persistence (device count stayed 2, no player swap) + 5-player regression PASS.

### v1.1.3 (2026-08-02) — player-swap fix + landscape rotation
- Server: 2 s timeout keeps ViGEm device alive (zeroes + logs once via `_zeroed[]`); new `ReleaseStaleControllers(30s)` destroys only on real disconnect.
- Client: `AndroidManifest` `landscape` → `sensorLandscape` (never portrait).
- versionCode 5.
- Verified: swap-test harness (P1 silent 6 s → 2 devices kept, no swap on revive) + 5-player regression PASS.

### v1.1.2 (2026-08-02) — input-chain fixes
8 root causes fixed ("joypad and buttons not responding correctly"):
1. Client stick Y inverted for XInput.
2. Stick clamp/normalize used dp where dragAmount is px → ~3x premature saturation.
3. Trigger divided px by dp + stale-value baseline accumulation → over-sensitive / could stick.
4. Button press had no `finally` → stuck button on gesture cancel.
5. Default layout triggers (y=0.09) overlapped LB/RB touch zones → moved to y=0.14.
6. SERVER: client timed out >2 s became a zombie — packets dropped forever (InputListener IsConnected gate after endpoint stayed mapped). Now revives on next packet.
7. Slot-reuse left stale endpoint mapping → hijack risk. Now `TryRemove` old endpoint mapping.
8. `InputListener.Dispose` crashed on disposed CTS. Hardened.
- versionCode 4.
- Verified end-to-end: XInputGetState harness (34-byte wire roundtrip byte-identical, all 14 button mappings, axes, triggers, zeroing ALL PASS) + zombie-revival test over real UDP PASS.

### v1.1.1 (2026-08-01)
- Layout editor completion, L3/R3 migration, server republished from repo, CI fixes.

### v1.1.0 (2026-08-01)
- **Hotspot actually starts.** Fixed a broken PowerShell script inside the server (a `//` comment was executed as code).
- **Works offline.** New virtual loopback adapter lets the hotspot start with zero physical NICs.
- **No more discovery spam.** 3 self-guards + dedupe stop the phantom-device log flood.
- **Stable logs.** 1 MB cap + rotation.
- Dirty tree committed as `146ab46`.

---

## Testing harnesses (dev machine, %TEMP%\opencode\)

- `swap-test` — real ViGEm + XInputGetState; sim phones; idle-timeout / player-swap / persistence tests.
- `mpv-test` — multi-player: 5 simulated phones + 1 real phone = Players 1–6; Windows enumerated 6 "Xbox 360 Controller for Windows" simultaneously. Games see Players 1–4 (XInput API indices 0-3 = Windows limit, not ours) → 4-player local co-op works. ViGEmBus supports >4 pads.

---

## RetroArch integration (dev machine)

D:\RetroArch portable 1.22.2, 197 cores, `input_joypad_driver = "xinput"` (works with ViGEm virtual pads). Games in `D:\RetroArch\games\snes|nes|n64|genesis|ps1|gb|gba|homebrew`. `launch.ps1` launcher. Verified: "[Autoconf] Xbox 360 Controller configured in port 1".

---

## Planned feature — keyboard / trackpad output modes (scoped 2026-08-02, NOT implemented)

Goal: (1) server outputs KEYBOARD instead of joypad, multiple players each assigned own key cluster; (2) in app, assign a keystroke to each button; (3) trackpad option + keep joypad option.

Locked decisions:
- Per-slot output mode: Joypad (existing ViGEm Xbox360) | Keyboard | Trackpad/Mouse.
- Keyboard injection via Win32 SendInput (StarkAgent-style), NOT Interception driver (kernel driver, overkill for v1; note as future option if per-player device separation needed).
- Caveat: SendInput sends to foreground/focused window — correct for shared-keyboard local multiplayer (P1=WASD, P2=arrows); anti-cheat games may reject synthetic input.
- App: extend existing layout editor — each button gets optional key binding (W/A/S/D/Space/arrows/modifiers); bindings persist in the same layout JSON LayoutManager already saves. Wire protocol unchanged for input (server already gets button bitmask → map bits→keys instead of →Xbox buttons).
- Trackpad: phone touch → SendInput mouse (relative move, tap=left click, 2-finger=right click/scroll).
- Config packet: PacketType.Config = 0x04 already declared-but-unused in Protocol.cs/Protocol.kt — use it to send mode + key bindings server-side. Keep contract byte-identical.
