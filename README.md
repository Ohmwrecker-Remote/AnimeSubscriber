# AnimeSubscriber

Anime RSS 订阅自动下载工具 — 监控 RSS 源更新，自动匹配番剧并通过 qBittorrent 下载。

## 功能

- **RSS 订阅管理** — 添加番剧 RSS 源，设定字幕组、画质偏好
- **自动下载** — 定时检查 RSS 更新，自动匹配并推送到 qBittorrent
- **手动选择** — 交互模式下逐个番剧选择要下载的剧集
- **下载状态** — 实时查看下载进度，支持选中删除任务
- **本地检测** — 自动跳过已下载的剧集，支持检测 .part 部分下载
- **标题解析** — 智能解析番剧名、集数、字幕组、画质（2160p/1080p/720p）
- **多候选排序** — 同集多个来源按画质、编码、字幕语言自动择优

## 环境要求

- Windows 10 / 11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [qBittorrent](https://www.qbittorrent.org/) (Web UI 需启用)

## 快速开始

1. 下载 [最新 Release](https://github.com/Ohmwrecker-Remote/AnimeSubscriber/releases/latest)，解压运行 `AnimeSubscriber.exe`
2. 在 **设置** 页面配置 qBittorrent 连接信息（主机、端口、用户名、密码）
3. 在 **订阅管理** 页面点击「添加 RSS」或「手动添加」，输入番剧 RSS 地址
4. 定时检查或手动触发检查，匹配的剧集将自动推送下载

## 配置说明

首次运行自动生成 `appsettings.json`：

```json
{
  "qbittorrent": {
    "host": "localhost",
    "port": 8080,
    "username": "admin",
    "password": "Please Enter Password",
    "savePath": "Choose Your Savepath",
    "category": "anime"
  },
  "settings": {
    "rssIntervalMinutes": 30,
    "proxy": ""
  }
}
```

| 字段 | 说明 |
|------|------|
| `qbittorrent.host` | qBittorrent Web UI 地址 |
| `qbittorrent.port` | Web UI 端口（默认 8080） |
| `qbittorrent.savePath` | 下载保存路径 |
| `qbittorrent.category` | qBittorrent 分类标签 |
| `settings.rssIntervalMinutes` | 自动检查间隔（分钟） |
| `settings.proxy` | HTTP 代理（可选，如 `http://127.0.0.1:<Your Port>`） |

## 项目结构

```
AnimeSubscriber/
├── Config/        # 配置读写
├── Converters/    # WPF 值转换器
├── Dialogs/       # 弹窗（剧集选择、手动添加）
├── Models/        # 数据模型
├── Services/      # 核心服务
│   ├── QBitService.cs   # qBittorrent Web API
│   ├── RssService.cs    # RSS 源抓取
│   ├── TitleParser.cs   # 番剧标题解析
│   ├── Matcher.cs       # 订阅规则匹配
│   ├── Ranker.cs        # 候选择优
│   ├── FileScanner.cs   # 本地文件检测
│   └── Logger.cs        # 异步日志
├── Themes/        # 主题样式
├── ViewModels/    # MVVM ViewModel
├── Views/         # WPF 页面
└── appsettings.json  # 用户配置
```

## 数据流程

```
RSS URL → RssService.FetchAsync()
  → TitleParser.Parse()         // 提取番剧名、集数、字幕组、画质
  → Matcher.Match()             // 按订阅规则筛选
  → FileScanner.GetEpisodeStatus() // 检查本地是否已存在
  → Ranker.PickBest()           // 多候选择优
  → QBitService.AddTorrentAsync() // 推送 qBittorrent
```

## 构建

```bash
git clone https://github.com/Ohmwrecker-Remote/AnimeSubscriber.git
cd AnimeSubscriber
dotnet build
dotnet run
```

## 技术栈

- .NET 8.0 / WPF / MVVM
- qBittorrent Web API v2
- System.Text.Json / HttpClient
- Regex-based title parsing
