using System.Windows.Input;
using AnimeSubscriber.Services;

namespace AnimeSubscriber.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly MainViewModel _owner;

    // ── qBittorrent ──
    private string _qbHost = "localhost";
    public string QbHost { get => _qbHost; set => Set(ref _qbHost, value); }

    private int _qbPort = 8081;
    public int QbPort { get => _qbPort; set => Set(ref _qbPort, value); }

    private string _qbUser = "admin";
    public string QbUser { get => _qbUser; set => Set(ref _qbUser, value); }

    private string _qbPass = "";
    public string QbPass { get => _qbPass; set => Set(ref _qbPass, value); }

    private string _qbPath = "";
    public string QbPath { get => _qbPath; set => Set(ref _qbPath, value); }

    private string _qbCategory = "anime";
    public string QbCategory { get => _qbCategory; set => Set(ref _qbCategory, value); }

    // ── Proxy ──
    private string _proxy = "";
    public string Proxy { get => _proxy; set => Set(ref _proxy, value); }

    // ── Polling ──
    private int _interval = 30;
    public int Interval { get => _interval; set => Set(ref _interval, value); }

    private bool _autoPoll = true;
    public bool AutoPoll { get => _autoPoll; set => Set(ref _autoPoll, value); }

    public ICommand BrowsePathCommand { get; }
    public ICommand SaveCommand { get; }

    public SettingsViewModel(MainViewModel owner)
    {
        _owner = owner;

        var qb = _owner.Config.QBittorrent;
        QbHost = qb.Host;
        QbPort = qb.Port;
        QbUser = qb.Username;
        QbPass = qb.Password;
        QbPath = qb.SavePath;
        QbCategory = qb.Category;
        Proxy = _owner.Config.Settings.Proxy;
        Interval = _owner.Config.Settings.RssIntervalMinutes;
        AutoPoll = true;

        BrowsePathCommand = new RelayCommand(BrowsePath);
        SaveCommand = new RelayCommand(async () => await SaveSettings());
    }

    private void BrowsePath()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = QbPath
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            QbPath = dialog.SelectedPath;
    }

    private async Task SaveSettings()
    {
        _owner.Config.QBittorrent.Host = QbHost.Trim();
        _owner.Config.QBittorrent.Port = QbPort;
        _owner.Config.QBittorrent.Username = QbUser.Trim();
        _owner.Config.QBittorrent.Password = QbPass;
        _owner.Config.QBittorrent.SavePath = QbPath.Trim();
        _owner.Config.QBittorrent.Category = QbCategory.Trim();

        _owner.Config.Settings.Proxy = Proxy.Trim();
        _owner.Config.Settings.RssIntervalMinutes = Interval;

        await _owner.Config.SaveAsync(_owner.ConfigPath);

        _owner.RecreateQBit(QbHost.Trim(), QbPort, QbUser.Trim(), QbPass);
        _owner.RecreateRss(string.IsNullOrWhiteSpace(Proxy) ? null : Proxy.Trim());
        await _owner.ConnectQBitAsync();

        System.Windows.MessageBox.Show("设置已保存", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }
}
