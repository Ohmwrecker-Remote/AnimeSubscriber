# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build     # Build (net8.0-windows, WinForms)
dotnet run       # Launch the app
```

Zero warnings and zero errors required for every build.

## Architecture

Anime RSS subscriber that monitors RSS feeds for new anime episodes and auto-downloads via qBittorrent Web API.

```
Program.cs     →  Entry point, runs MainForm
MainForm.cs    →  4-page sidebar layout (订阅管理 / 下载状态 / 设置 / 日志)
                  All UI built programmatically (no designer files)
Theme.cs       →  Design system: color palette, typography, custom controls
                  (ModernButton, NavButton, RoundedPanel), StyleListBox/StyleListView
Config/        →  AppConfig — JSON read/write with async FileStream support
Models/        →  Subscription, RssItem, ParsedTitle, DownloadEntry, EpisodeEntry
Services/      →  QBitService, RssService, TitleParser, Matcher, Ranker,
                  FileScanner, Logger
Dialogs/       →  EpisodeSelectDialog — modal with custom-painted checkable rows
```

### Data Flow

```
RSS URL → RssService.FetchAsync() → XML → List<RssItem>
  → TitleParser.Parse() → ParsedTitle (subgroup, anime name, episode, quality)
  → Matcher.Match() → filters by subscription criteria
  → FileScanner.GetEpisodeStatus() → checks local disk
  → Ranker.PickBest() → scores by quality+codec+subtitle
  → QBitService.AddTorrentAsync() → qBittorrent Web API
```

### Two Check Paths

- **Manual** (buttons): `CheckAllSubscriptionsInteractive()` → parallel fetch → `EpisodeSelectDialog` → user picks → download selected
- **Auto-poll** (timer): `CheckAllSubscriptions()` → parallel fetch → auto-rank → auto-download best version — no dialog

### Theme System

All visual styling flows through `Theme.cs`. No hardcoded colors in UI code — always use `Theme.AccentBlue`, `Theme.BgCard`, `Theme.TextPrimary`, etc.

Custom controls:
- `ModernButton` — rounded corners, animated hover (16ms timer), `SmallRadius` (8px)
- `NavButton` — sidebar button with 4px accent bar when active, no hover animation
- `RoundedPanel` — rounded corners (`CornerRadius` 12px) + subtle shadow + hairline border

Colors follow Claude DESIGN.md warm cream palette: canvas `#faf9f5`, coral primary `#cc785c`, dark navy sidebar `#181715`.

### Logger

Async channel-based: `Channel<string>` with single background `StreamWriter`. Call `Logger.FlushAndStopAsync()` on shutdown. Log file: `logs/log.txt` (auto-rotates at 5MB).

### Config

`appsettings.json` — auto-created with defaults if missing. Settings page now exposes qBittorrent credentials in the UI (previously JSON-only). Uses `System.Text.Json` with `PropertyNameCaseInsensitive` and `WriteIndented`.

### TitleParser

Uses 6 `[GeneratedRegex]` patterns to extract subgroup, anime name, and episode number from RSS item titles. Has a fallback path that logs warnings — check logs when parsing fails.

## Key Constraints

- All UI is WinForms GDI+ custom-drawn — NO WebView2, NO WPF
- Font: `Microsoft YaHei UI` for Chinese text, `JetBrains Mono`/`Consolas` for code/logs
- Chinese UI labels throughout — `PlaceholderText`, button text, dialog messages
- Config uses camelCase JSON via `JsonNamingPolicy.CamelCase`
