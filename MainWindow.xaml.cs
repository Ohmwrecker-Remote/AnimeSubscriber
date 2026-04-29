using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AnimeSubscriber.Config;
using AnimeSubscriber.Services;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly string _configPath;
    private System.Timers.Timer? _autoTimer;

    public MainWindow()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        var config = AppConfig.Load(_configPath);

        _vm = new MainViewModel(config, _configPath);
        _vm.StatusBarUpdated += UpdateStatusBar;
        DataContext = _vm;

        InitializeComponent();

        // Wire nav button styles
        WireNavButtons();

        Loaded += async (_, _) =>
        {
            Logger.Info("应用启动");
            await _vm.ConnectQBitAsync();
            UpdateSidebarStatus();
            UpdateStatusBar();
            StartAutoTimer();
        };

        Closing += async (_, _) =>
        {
            Logger.Info("应用关闭");
            _autoTimer?.Dispose();
            _vm.QBit.Dispose();
            _vm.Rss.Dispose();
            await Logger.FlushAndStopAsync();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPage))
                UpdateNavButtonStyles();
            else if (e.PropertyName == nameof(MainViewModel.SidebarStatusText))
                UpdateSidebarStatus();
        };
    }

    private void WireNavButtons()
    {
        BtnSubs.Style = (Style)FindResource("ActiveNavButton");
        BtnDownloads.Style = (Style)FindResource("NavButton");
        BtnSettings.Style = (Style)FindResource("NavButton");
        BtnLogs.Style = (Style)FindResource("NavButton");
    }

    private void UpdateNavButtonStyles()
    {
        var pageName = _vm.CurrentPage.GetType().Name;
        BtnSubs.Style = (Style)FindResource(pageName == "SubscriptionsActive" ? "ActiveNavButton" : "NavButton");
        BtnDownloads.Style = (Style)FindResource(pageName == "DownloadsActive" ? "ActiveNavButton" : "NavButton");
        BtnSettings.Style = (Style)FindResource(pageName == "SettingsActive" ? "ActiveNavButton" : "NavButton");
        BtnLogs.Style = (Style)FindResource(pageName == "LogsActive" ? "ActiveNavButton" : "NavButton");
    }

    public void UpdateSidebarStatus()
    {
        SidebarStatus.Text = _vm.SidebarStatusText;
        SidebarStatus.Foreground = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_vm.SidebarStatusColor));
    }

    public void UpdateStatusBar()
    {
        var subCount = _vm.Config.Subscriptions.Count;
        StatusEntries.Text = $"共 {subCount} 个订阅";
        StatusNextCheck.Text = _vm.NextCheckText;
    }

    public void StartAutoTimer()
    {
        _autoTimer?.Dispose();
        var settings = _vm.Config.Settings;
        _autoTimer = new System.Timers.Timer(settings.RssIntervalMinutes * 60 * 1000);
        _autoTimer.Elapsed += async (_, _) =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                var subPage = FindVisualChild<AnimeSubscriber.Views.SubscriptionsPage>(this);
                if (subPage?.ViewModel != null)
                    await subPage.ViewModel.CheckAllAsync();
            });
        };
        _autoTimer.AutoReset = true;
        _autoTimer.Start();

        _vm.NextCheckText = $"下次检查: {DateTime.Now.AddMinutes(settings.RssIntervalMinutes):HH:mm}";
    }

    public MainViewModel ViewModel => _vm;

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return default;
    }
}
