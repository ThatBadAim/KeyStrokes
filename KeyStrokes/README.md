# KeyStrokes

A native, local-first keystroke telemetry and productivity tracker for Windows 11 — built with **WPF on .NET 10** and a low-level Win32 keyboard hook. It counts *how much* and *how fast* you type (per-key frequencies, KPM, daily trends) without ever recording *what* you type.

> **Privacy by design.** KeyStrokes stores only aggregate counts — never the order keys were pressed — so typed content (passwords, messages) cannot be reconstructed from its data. It makes **zero network calls**, and it automatically pauses when a password manager or sign-in window is focused.

---

## Features

| Module | What it does |
| --- | --- |
| **Live dashboard** | Animated odometer counters (lifetime / session / today), a live KPM speedometer gauge with an eased needle, top-5 keys, and session stats. |
| **Master toggle** | A prominent switch. When off, the keyboard hook is **fully removed** — zero capture, zero CPU. |
| **Breakdown** | A searchable, sortable grid of every key with exact counts, category, and share of total (scope: all-time / today / session). |
| **Heatmap** | A stylized virtual keyboard whose keys glow from cold blue → hot neon pink by usage frequency. |
| **History** | Daily / weekly / monthly trend bars with totals, busiest period, daily average, and active-day count. |
| **System tray** | Closing the window minimizes to the tray; right-click to pause/resume or exit. |
| **Export** | Formatted CSV (key breakdown) and JSON (full data). |
| **Privacy exclusions** | Configurable list of protected apps and window-title keywords that suspend capture. |

## Design

- **Fluent / Windows 11 aesthetics:** native **Mica** backdrop (via DWM), custom window chrome, rounded corners, and a dark monochrome-slate palette (`#0B0F19`) with electric-indigo → neon-pink accents.
- **Bento grid** dashboard with glass cards that let the Mica backdrop show through the gaps.
- **60 FPS micro-interactions:** counters ease to new values; the KPM needle animates smoothly; the master toggle thumb springs.

## Architecture

```
Interop/        Win32 P/Invoke + the dedicated-thread input monitor (WH_KEYBOARD_LL + WinEvent)
Services/       TrackingService (engine), StorageService (atomic JSON), PrivacyService,
                KeyMapper, ExportService
Models/         AppData (persistence) + observable UI models
ViewModels/     MVVM: MainViewModel shell + one PageViewModel per view
Controls/       AnimatedNumber (odometer), KpmGauge (speedometer)
Views/          Dashboard / Breakdown / Heatmap / History / Settings (UserControls)
Themes/         Palette.xaml + Styles.xaml (all control styling)
```

**Zero input lag.** The low-level keyboard hook runs on its own background thread with a private Win32 message pump. Its callback is O(1) — it only reads a couple of volatile flags and raises an event. Counting happens lock-free (`ConcurrentDictionary` + interlocked totals) on that thread; the UI merely polls the current values a few times a second, so heavy typing or gaming is never blocked behind the WPF UI thread.

**No corruption.** Persistence uses `System.Text.Json` (BCL, no external packages) with atomic writes (temp file + replace) and a rolling `.bak`. Data auto-saves every 15 s and commits gracefully on exit.

## Requirements

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (the WindowsDesktop runtime)

No NuGet packages and no Windows App SDK workload are required.

## Build & run

```powershell
dotnet run --project KeyStrokes.csproj -c Release
```

or build an executable:

```powershell
dotnet build -c Release
# -> bin/Release/net10.0-windows/KeyStrokes.exe
```

A self-contained single file:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Where data lives

`%APPDATA%\KeyStrokes\data.json` (with `data.json.bak`). Delete it to reset all statistics.

## Packaging & signing (optional next step)

The project is structured for clean MSIX packaging. To distribute warning-free:

1. Add a **Windows Application Packaging Project** referencing `KeyStrokes.csproj`.
2. In `Package.appxmanifest`, declare **no capabilities** (KeyStrokes needs none — reinforcing the air-gapped guarantee).
3. Sign with `signtool` using a code-signing certificate.

Because the app is dual-use (it functions like a keylogger), signing and a no-network manifest are what let it deploy without SmartScreen/AV friction.

## A note on responsible use

KeyStrokes is a **self-monitoring** tool: run it on your own machine to understand your own typing. It is intentionally transparent (visible UI + tray icon), consensual (a master switch that fully unhooks), local-only, and privacy-preserving (aggregate counts only, sensitive-field exclusions). Do not use it to monitor other people without their knowledge and consent.
