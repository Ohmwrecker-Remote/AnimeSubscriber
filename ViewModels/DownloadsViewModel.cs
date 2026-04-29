using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AnimeSubscriber.Models;
using AnimeSubscriber.Services;

namespace AnimeSubscriber.ViewModels;

public class DownloadsViewModel : BaseViewModel
{
    private readonly MainViewModel _owner;
    private DispatcherTimer? _pollTimer;

    public ObservableCollection<DownloadRow> Items { get; } = new();

    private bool _loading;
    public bool Loading
    {
        get => _loading;
        set { Set(ref _loading, value); OnPropertyChanged(nameof(IsNotLoading)); }
    }
    public bool IsNotLoading => !Loading;

    public ICommand RefreshCommand { get; }
    public ICommand CheckNowCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ToggleRowCommand { get; }
    public ICommand DeleteKeyCommand { get; }

    private readonly object _statusLock = new();
    private Dictionary<string, DownloadStatus> _prevStatus = new();

    public DownloadsViewModel(MainViewModel owner)
    {
        _owner = owner;
        Items.Add(PlaceholderRow());
        RefreshCommand = new RelayCommand(async () => await PollQBitAsync());
        CheckNowCommand = new RelayCommand(async () =>
        {
            var subsVm = new SubscriptionsViewModel(_owner);
            await subsVm.CheckNowInteractive();
            await PollQBitAsync();
        });
        DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedAsync());
        SelectAllCommand = new RelayCommand(ToggleSelectAll);
        ToggleRowCommand = new RelayCommand<DownloadRow>(row =>
        {
            if (row != null && !string.IsNullOrEmpty(row.Hash))
                row.IsSelected = !row.IsSelected;
        });
        DeleteKeyCommand = new RelayCommand(async () => await DeleteSelectedAsync());

        _ = PollQBitAsync();
    }

    // ── Polling ──

    public void StartDownloadPolling()
    {
        if (_pollTimer?.IsEnabled == true) return;

        StopDownloadPolling();
        _pollTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(3),
            DispatcherPriority.Background,
            OnPollTick,
            Application.Current.Dispatcher);
        _pollTimer.Start();
    }

    public void StopDownloadPolling()
    {
        _pollTimer?.Stop();
        _pollTimer = null;
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        _ = PollActiveAsync();
    }

    private async Task PollActiveAsync()
    {
        if (Loading || !_owner.IsConnected) return;

        Loading = true;
        try
        {
            var torrents = await _owner.QBit.GetTorrentsAsync();

            // Detect completion transitions
            var newStatuses = torrents.ToDictionary(t => t.Hash, t => t.Status);
            Dictionary<string, DownloadStatus> prevCopy;
            lock (_statusLock) { prevCopy = new(_prevStatus); _prevStatus = newStatuses; }
            foreach (var (hash, prevStatus) in prevCopy)
            {
                if (prevStatus == DownloadStatus.Downloading &&
                    newStatuses.TryGetValue(hash, out var ns) && ns == DownloadStatus.Completed)
                {
                    var name = torrents.FirstOrDefault(x => x.Hash == hash)?.AnimeName ?? hash;
                    NotificationService.Show("下载完成", $"{name} 已下载完成");
                }
            }

            ApplyTorrentUpdate(torrents);

            // Check after update: stop if no active torrents remain
            var hasActive = Items.Any(r => r.Hash != "" &&
                (r.Status == DownloadStatus.Downloading || r.Status == DownloadStatus.Waiting));
            if (!hasActive)
                StopDownloadPolling();
        }
        catch (Exception ex) { Logger.Error("轮询下载状态失败", ex); _owner.ShowError("下载状态刷新失败"); }
        finally { Loading = false; }
    }

    public async Task PollQBitAsync()
    {
        if (Loading || !_owner.IsConnected) return;
        Loading = true;
        try
        {
            var torrents = await _owner.QBit.GetTorrentsAsync();
            lock (_statusLock) { _prevStatus = torrents.ToDictionary(t => t.Hash, t => t.Status); }
            ApplyTorrentUpdate(torrents);

            var sorted = torrents
                .OrderByDescending(t => t.Status == DownloadStatus.Downloading)
                .ThenBy(t => t.AnimeName)
                .ThenBy(t => t.Episode)
                .ToList();

            if (sorted.Any(t => t.Status == DownloadStatus.Downloading
                             || t.Status == DownloadStatus.Waiting))
                StartDownloadPolling();
        }
        catch (Exception ex) { Logger.Error("轮询下载状态失败", ex); _owner.ShowError("下载状态刷新失败"); }
        finally { Loading = false; }
    }

    // ── Incremental update ──

    private void ApplyTorrentUpdate(List<DownloadEntry> torrents)
    {
        var newMap = torrents.ToDictionary(t => t.Hash);

        // Remove rows no longer in qBit
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(Items[i].Hash) && !newMap.ContainsKey(Items[i].Hash))
                Items.RemoveAt(i);
        }

        // Remove placeholder if we have real data
        var placeholder = Items.FirstOrDefault(r => string.IsNullOrEmpty(r.Hash));
        if (placeholder != null && torrents.Count > 0)
            Items.Remove(placeholder);

        // Update existing rows by replacing them (triggers UI refresh via CollectionChanged)
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            var row = Items[i];
            if (!string.IsNullOrEmpty(row.Hash) && newMap.TryGetValue(row.Hash, out var entry))
            {
                var updated = MakeRow(entry);
                updated.IsSelected = row.IsSelected; // preserve selection
                Items[i] = updated;
            }
        }

        // Add new rows
        foreach (var entry in torrents)
        {
            if (!Items.Any(r => r.Hash == entry.Hash))
                Items.Add(MakeRow(entry));
        }

        // Re-sort in place
        var sorted = Items.Where(r => r.Hash != "").ToList();
        sorted.Sort((a, b) =>
        {
            int cmp = b.Status.CompareTo(a.Status); // downloading first
            if (cmp != 0) return cmp;
            cmp = string.CompareOrdinal(a.AnimeName, b.AnimeName);
            if (cmp != 0) return cmp;
            return string.CompareOrdinal(a.Episode, b.Episode);
        });

        for (int i = 0; i < sorted.Count; i++)
        {
            var cur = Items.IndexOf(sorted[i]);
            if (cur != i) Items.Move(cur, i);
        }

        // Restore placeholder if empty
        if (Items.Count == 0)
            Items.Add(PlaceholderRow());
    }

    private static DownloadRow MakeRow(DownloadEntry t) => new()
    {
        Hash = t.Hash,
        AnimeName = t.AnimeName,
        Episode = t.Episode > 0 ? t.Episode.ToString("D2") : "—",
        Subgroup = t.Subgroup,
        Quality = t.Quality,
        Progress = t.Progress * 100,
        StatusText = t.StatusText,
        Status = t.Status
    };

    private static DownloadRow PlaceholderRow() => new()
    {
        AnimeName = "暂无下载任务",
        Episode = "--", Subgroup = "--", Quality = "--",
        Progress = 0, StatusText = "—"
    };

    // ── Selection ──

    private void ToggleSelectAll()
    {
        var real = Items.Where(r => r.Hash != "").ToList();
        if (real.Count == 0) return;
        var allSelected = real.All(r => r.IsSelected);
        foreach (var r in real)
            r.IsSelected = !allSelected;
    }

    private async Task DeleteSelectedAsync()
    {
        var selected = Items.Where(r => r.IsSelected && !string.IsNullOrEmpty(r.Hash)).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先勾选要删除的下载任务", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
                $"确定删除选中的 {selected.Count} 个下载任务？\n这将同时删除下载的文件。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var hashes = selected.Select(r => r.Hash).ToList();
        var success = await _owner.QBit.DeleteTorrentsAsync(hashes, deleteFiles: true);

        if (success)
            await PollQBitAsync();
        else
            MessageBox.Show("删除失败，请检查 qBittorrent 连接状态", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

public class DownloadRow : INotifyPropertyChanged
{
    public string Hash { get; set; } = "";
    public string AnimeName { get; set; } = "";
    public string Episode { get; set; } = "";
    public string Subgroup { get; set; } = "";
    public string Quality { get; set; } = "";
    public double Progress { get; set; }
    public string StatusText { get; set; } = "";
    public DownloadStatus Status { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
