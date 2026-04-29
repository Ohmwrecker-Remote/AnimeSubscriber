using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AnimeSubscriber.Config;
using AnimeSubscriber.Services;
using AnimeSubscriber.Services.Abstractions;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public MainViewModel MainViewModel => (MainViewModel)MainWindow.DataContext;

    public static string AppConfigPath { get; } =
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // Config
        var config = AppConfig.Load(AppConfigPath);
        config.ConfigPath = AppConfigPath;
        services.AddSingleton<IConfigService>(config);

        // Services
        services.AddSingleton<ILogger, LoggerAdapter>();
        services.AddSingleton<ITitleParser>(TitleParser.Instance);
        services.AddSingleton<IMatcher>(Matcher.Instance);

        Ranker.Instance.PreferCHS = config.Downloader.PreferCHS;
        Ranker.Instance.PreferMKV = config.Downloader.PreferMKV;
        Ranker.Instance.PreferHEVC = config.Downloader.PreferHEVC;
        services.AddSingleton<IRanker>(Ranker.Instance);
        services.AddSingleton<IFileScanner>(FileScanner.Instance);
        services.AddSingleton<IQBitService>(sp =>
        {
            var qb = config.QBittorrent;
            return new QBitService(qb.Host, qb.Port, qb.Username, qb.Password);
        });
        services.AddSingleton<IRssService>(sp =>
        {
            var proxy = string.IsNullOrEmpty(config.Settings.Proxy) ? null : config.Settings.Proxy;
            return new RssService(proxy);
        });

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SubscriptionsViewModel>();
        services.AddTransient<DownloadsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LogsViewModel>();

        Services = services.BuildServiceProvider();

        var mainVm = Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow(mainVm);
        MainWindow = window;
        window.Show();

        base.OnStartup(e);
    }
}
