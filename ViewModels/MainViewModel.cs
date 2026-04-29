using System.Windows.Input;
using AnimeSubscriber.Config;
using AnimeSubscriber.Services;

namespace AnimeSubscriber.ViewModels;

public class MainViewModel : BaseViewModel
{
    // ── Navigation sentinel types ──
    public sealed class SubscriptionsActive { }
    public sealed class DownloadsActive { }
    public sealed class SettingsActive { }
    public sealed class LogsActive { }

    private object _currentPage = new SubscriptionsActive();
    public object CurrentPage
    {
        get => _currentPage;
        set => Set(ref _currentPage, value);
    }

    public ICommand NavCommand { get; }

    // ── Shared services ──
    public AppConfig Config { get; }
    public QBitService QBit { get; private set; }
    public RssService Rss { get; private set; }

    // ── Shared state ──
    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => Set(ref _isConnected, value);
    }

    private string _statusText = "共 0 个订阅";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private string _nextCheckText = "下次检查: --";
    public string NextCheckText
    {
        get => _nextCheckText;
        set => Set(ref _nextCheckText, value);
    }

    private string _sidebarStatusText = "● 未连接";
    public string SidebarStatusText
    {
        get => _sidebarStatusText;
        set => Set(ref _sidebarStatusText, value);
    }

    public string _sidebarStatusColor = "#a09d96";
    public string SidebarStatusColor
    {
        get => _sidebarStatusColor;
        set => Set(ref _sidebarStatusColor, value);
    }

    public string ConfigPath { get; private set; } = "";

    public event Action? StatusBarUpdated;

    public MainViewModel(AppConfig config, string configPath)
    {
        Config = config;
        ConfigPath = configPath;

        var qb = config.QBittorrent;
        QBit = new QBitService(qb.Host, qb.Port, qb.Username, qb.Password);
        Rss = new RssService(string.IsNullOrEmpty(config.Settings.Proxy) ? null : config.Settings.Proxy);

        NavCommand = new RelayCommand<string>(page =>
        {
            CurrentPage = page switch
            {
                "subs" => new SubscriptionsActive(),
                "downloads" => new DownloadsActive(),
                "settings" => new SettingsActive(),
                "logs" => new LogsActive(),
                _ => CurrentPage
            };
        });
    }

    public void UpdateStatusCounts()
    {
        StatusText = $"共 {Config.Subscriptions.Count} 个订阅";
        StatusBarUpdated?.Invoke();
    }

    public void UpdateNextCheckTime()
    {
        NextCheckText = $"下次检查: {DateTime.Now.AddMinutes(Config.Settings.RssIntervalMinutes):HH:mm}";
        StatusBarUpdated?.Invoke();
    }

    public void RecreateQBit(string host, int port, string user, string pass)
    {
        QBit.Dispose();
        QBit = new QBitService(host, port, user, pass);
    }

    public void RecreateRss(string? proxy)
    {
        Rss.Dispose();
        Rss = new RssService(string.IsNullOrEmpty(proxy) ? null : proxy);
    }

    public async Task ConnectQBitAsync()
    {
        await QBit.ConnectAsync();
        IsConnected = QBit.IsConnected;
        SidebarStatusText = IsConnected ? "● 已连接" : "● 未连接";
        SidebarStatusColor = IsConnected ? "#5db872" : "#a09d96";
    }
}
