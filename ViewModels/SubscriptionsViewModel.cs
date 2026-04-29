using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AnimeSubscriber.Dialogs;
using AnimeSubscriber.Models;
using AnimeSubscriber.Services;

namespace AnimeSubscriber.ViewModels;

public class SubscriptionsViewModel : BaseViewModel
{
    private readonly MainViewModel _owner;

    public ObservableCollection<SubscriptionDisplay> Subs { get; } = new();

    private string _rssUrl = "";
    public string RssUrl
    {
        get => _rssUrl;
        set => Set(ref _rssUrl, value);
    }

    private SubscriptionDisplay? _selectedSub;
    public SubscriptionDisplay? SelectedSub
    {
        get => _selectedSub;
        set
        {
            Set(ref _selectedSub, value);
            OnPropertyChanged(nameof(CanDelete));
        }
    }
    public bool CanDelete => SelectedSub != null;

    private bool _loading;
    public bool Loading
    {
        get => _loading;
        set { Set(ref _loading, value); OnPropertyChanged(nameof(IsNotLoading)); }
    }
    public bool IsNotLoading => !Loading;

    public ICommand AddRssCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CheckNowCommand { get; }

    public SubscriptionsViewModel(MainViewModel owner)
    {
        _owner = owner;
        AddRssCommand = new RelayCommand(async () => await AddRssSubscription());
        DeleteCommand = new RelayCommand(DeleteSubscription, () => CanDelete);
        CheckNowCommand = new RelayCommand(async () => await CheckNowInteractive());
    }

    public void RefreshList()
    {
        Subs.Clear();
        foreach (var sub in _owner.Config.Subscriptions)
        {
            var meta = sub.LastCheckTime.HasValue
                ? $"  ·  上次检查: {FormatRelativeTime(sub.LastCheckTime.Value)}"
                : "";
            Subs.Add(new SubscriptionDisplay(sub, $"{sub.Name}{meta}"));
        }
        _owner.UpdateStatusCounts();
    }

    // ── Auto-poll (no dialog) ──

    public async Task CheckAllAutoAsync()
    {
        await CheckAllAutoAsyncImpl();
    }

    public async Task CheckAllAsync() => await CheckAllAutoAsyncImpl();

    private async Task CheckAllAutoAsyncImpl()
    {
        if (_owner.Config.Subscriptions.Count == 0) return;
        Logger.Info($"自动检查 {_owner.Config.Subscriptions.Count} 个订阅");
        Loading = true;

        var limit = Math.Max(1, _owner.Config.Downloader.ConcurrencyLimit);
        var opts = new ParallelOptions { MaxDegreeOfParallelism = limit };
        await Parallel.ForEachAsync(_owner.Config.Subscriptions, opts, async (sub, _) =>
        {
            try { await CheckOneAndDownloadAsync(sub); }
            catch (Exception ex) { Logger.Error($"[{sub.Name}] 检查失败", ex); _owner.ShowError($"检查失败: {sub.Name}"); }
        });
        _owner.Config.Save(_owner.ConfigPath);
        Loading = false;
        RefreshList();
        _owner.UpdateNextCheckTime();
    }

    private async Task CheckOneAndDownloadAsync(Subscription sub)
    {
        sub.LastCheckTime = DateTime.Now;
        var items = await _owner.Rss.FetchAsync(sub.RssUrl);
        var saveDir = Path.Combine(_owner.Config.QBittorrent.SavePath, sub.SaveSubfolder);

        var matched = new List<(RssItem Item, ParsedTitle Parsed)>();
        foreach (var item in items)
        {
            var parsed = TitleParser.Parse(item.Title);
            if (parsed?.Episode == null) continue;
            if (!Matcher.Match(sub, parsed)) continue;
            matched.Add((item, parsed));
        }

        var groups = matched.GroupBy(m => m.Parsed.Episode ?? 0).OrderBy(g => g.Key);

        var newCount = 0;
        foreach (var group in groups)
        {
            var entries = group.ToList();
            if (FileScanner.GetEpisodeStatus(saveDir, group.Key) != DownloadStatus.Waiting) continue;

            var best = Ranker.PickBest(entries);
            if (best == null) continue;
            var (bestItem, _) = best.Value;
            var ok = await _owner.QBit.AddTorrentAsync(bestItem.TorrentUrl, saveDir, _owner.Config.QBittorrent.Category);
            if (ok) newCount++;
        }

        if (newCount > 0)
            NotificationService.Show("RSS 更新", $"[{sub.Name}] 发现 {newCount} 个新剧集，已推送到下载队列");
    }

    // ── Interactive check (with episode selector) ──

    public async Task CheckNowInteractive()
    {
        if (_owner.Config.Subscriptions.Count == 0)
        {
            MessageBox.Show("没有订阅可供检查", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Loading = true;
        var allMatches = await FetchAllSubscriptionsAsync();
        _owner.Config.Save(_owner.ConfigPath);
        Loading = false;

        var waiting = allMatches
            .Where(m => m.Status == DownloadStatus.Waiting)
            .OrderBy(m => m.Entry.Episode)
            .Select(m => m.Entry)
            .ToList();

        if (waiting.Count == 0)
        {
            RefreshList();
            MessageBox.Show("所有剧集已是最新，没有待下载的内容", "检查完成", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new EpisodeSelectDialog(waiting)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            MinWidth = 520, MinHeight = 420,
        };

        if (dialog.ShowDialog() != true || dialog.SelectedEntries.Count == 0)
        {
            RefreshList();
            _owner.UpdateNextCheckTime();
            return;
        }

        Loading = true;
        var epToMatch = allMatches.ToDictionary(m => m.Entry, m => m);

        foreach (var entry in dialog.SelectedEntries)
        {
            if (!epToMatch.TryGetValue(entry, out var match)) continue;
            await _owner.QBit.AddTorrentAsync(match.Entry.TorrentUrl, match.SaveDir, _owner.Config.QBittorrent.Category);
        }

        Loading = false;
        RefreshList();
        _owner.UpdateNextCheckTime();
    }

    private async Task<List<MatchedEpisode>> FetchAllSubscriptionsAsync()
    {
        var allMatches = new ConcurrentBag<MatchedEpisode>();
        var limit = Math.Max(1, _owner.Config.Downloader.ConcurrencyLimit);
        var opts = new ParallelOptions { MaxDegreeOfParallelism = limit };

        await Parallel.ForEachAsync(_owner.Config.Subscriptions, opts, async (sub, _) =>
        {
            try
            {
                var results = await FetchOneAsync(sub);
                foreach (var r in results) allMatches.Add(r);
            }
            catch (Exception ex) { Logger.Error($"[{sub.Name}] 检查失败", ex); _owner.ShowError($"检查失败: {sub.Name}"); }
        });

        return allMatches.ToList();
    }

    private async Task<List<MatchedEpisode>> FetchOneAsync(Subscription sub)
    {
        var results = new List<MatchedEpisode>();
        var items = await _owner.Rss.FetchAsync(sub.RssUrl);
        var saveDir = Path.Combine(_owner.Config.QBittorrent.SavePath, sub.SaveSubfolder);

        foreach (var item in items)
        {
            var parsed = TitleParser.Parse(item.Title);
            if (parsed?.Episode == null) continue;
            if (!Matcher.Match(sub, parsed)) continue;

            var status = FileScanner.GetEpisodeStatus(saveDir, parsed.Episode.Value);
            var entry = new EpisodeEntry(
                Title: parsed.AnimeName,
                Episode: parsed.Episode.Value,
                Quality: parsed.Quality ?? "",
                Subgroup: parsed.Subgroup ?? "",
                TorrentUrl: item.TorrentUrl,
                SubscriptionName: sub.Name
            );
            results.Add(new MatchedEpisode(sub, entry, status, saveDir));
        }
        sub.LastCheckTime = DateTime.Now;
        return results;
    }

    // ── Add RSS ──

    private async Task AddRssSubscription()
    {
        var url = RssUrl.Trim();
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http"))
        {
            MessageBox.Show("请输入有效的 RSS 链接", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        List<RssItem> items;
        try
        {
            Loading = true;
            items = await _owner.Rss.FetchAsync(url);
            Loading = false;
        }
        catch (Exception ex)
        {
            Loading = false;
            MessageBox.Show($"RSS 抓取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (items.Count == 0)
        {
            MessageBox.Show("RSS 中没有找到任何条目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entries = new List<EpisodeEntry>();
        foreach (var item in items)
        {
            var parsed = TitleParser.Parse(item.Title);
            if (parsed?.Episode == null) continue;
            entries.Add(new EpisodeEntry(
                Title: item.Title,
                Episode: parsed.Episode.Value,
                Quality: parsed.Quality ?? "",
                Subgroup: parsed.Subgroup ?? "",
                TorrentUrl: item.TorrentUrl
            ));
        }

        if (entries.Count == 0)
        {
            MessageBox.Show("未能解析到任何剧集信息", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        entries = entries.OrderBy(e => e.Episode).ToList();
        var name = entries.First().Title;
        var parsedName = TitleParser.Parse(name);
        if (parsedName != null)
        {
            name = parsedName.AnimeName;
            var slashIdx = name.IndexOf('/');
            if (slashIdx > 0) name = name[..slashIdx].Trim();
        }

        var inputDialog = new ManualAddDialog("添加 RSS 订阅", name);
        inputDialog.Owner = Application.Current.MainWindow;
        if (inputDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(inputDialog.SubName))
            return;

        var subName = inputDialog.SubName.Trim();
        var epDialog = new EpisodeSelectDialog(entries)
        {
            Owner = Application.Current.MainWindow,
            Title = $"选择要下载的剧集 — {subName}",
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            MinWidth = 520, MinHeight = 420,
        };

        if (epDialog.ShowDialog() != true || epDialog.SelectedEntries.Count == 0) return;

        var sub = new Subscription { Name = subName, RssUrl = url, SaveSubfolder = subName };
        _owner.Config.Subscriptions.Add(sub);
        _owner.Config.Save(_owner.ConfigPath);
        RssUrl = "";
        Logger.Info($"添加订阅: {sub.Name}, 已选择 {epDialog.SelectedEntries.Count} 集");

        Loading = true;
        var saveDir = Path.Combine(_owner.Config.QBittorrent.SavePath, sub.SaveSubfolder);
        foreach (var entry in epDialog.SelectedEntries)
            await _owner.QBit.AddTorrentAsync(entry.TorrentUrl, saveDir, _owner.Config.QBittorrent.Category);

        Loading = false;
        RefreshList();
        MessageBox.Show($"已添加 {epDialog.SelectedEntries.Count} 集到下载队列", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteSubscription()
    {
        if (SelectedSub == null) return;
        var sub = SelectedSub.Sub;
        if (MessageBox.Show($"确定删除订阅 \"{sub.Name}\"？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _owner.Config.Subscriptions.Remove(sub);
        _owner.Config.Save(_owner.ConfigPath);
        RefreshList();
    }

    private static string FormatRelativeTime(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalMinutes < 1) return "刚刚";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}分钟前";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}小时前";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}天前";
        return time.ToString("MM-dd");
    }
}

public class SubscriptionDisplay
{
    public Subscription Sub { get; }
    public string DisplayText { get; }

    public SubscriptionDisplay(Subscription sub, string displayText)
    {
        Sub = sub;
        DisplayText = displayText;
    }
}

public record MatchedEpisode(Subscription Sub, EpisodeEntry Entry, DownloadStatus Status, string SaveDir);
