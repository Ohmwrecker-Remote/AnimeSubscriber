using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AnimeSubscriber.Services;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private CancellationTokenSource? _timerCts;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        _vm.StatusBarUpdated += UpdateStatusBar;
        DataContext = _vm;

        InitializeComponent();

        WireNavButtons();

        Loaded += async (_, _) =>
        {
            Logger.Info("应用启动");
            NotificationService.Init();
            await _vm.ConnectQBitAsync();
            UpdateSidebarStatus();
            UpdateStatusBar();
            StartAutoTimer();
        };

        Closing += async (_, _) =>
        {
            Logger.Info("应用关闭");
            _timerCts?.Cancel();
            _vm.QBit.Dispose();
            _vm.Rss.Dispose();
            await Logger.FlushAndStopAsync();
            NotificationService.Dispose();
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
        _timerCts?.Cancel();
        _timerCts = new CancellationTokenSource();
        var ct = _timerCts.Token;
        var settings = _vm.Config.Settings;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.RssIntervalMinutes));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (_vm.AutoCheckRequested != null)
                        await _vm.AutoCheckRequested.Invoke();
                });
            }
        }, ct);

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
