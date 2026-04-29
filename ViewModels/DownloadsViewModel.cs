using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AnimeSubscriber.Models;
using AnimeSubscriber.Services;

namespace AnimeSubscriber.ViewModels;

public class DownloadsViewModel : BaseViewModel
{
    private readonly MainViewModel _owner;

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

    public DownloadsViewModel(MainViewModel owner)
    {
        _owner = owner;
        Items.Add(new DownloadRow { AnimeName = "暂无下载任务", Episode = "--", Subgroup = "--", Quality = "--", Progress = 0, StatusText = "—" });
        RefreshCommand = new RelayCommand(async () => await PollQBitAsync());
        CheckNowCommand = new RelayCommand(async () =>
        {
            var subsVm = new SubscriptionsViewModel(_owner);
            await subsVm.CheckNowInteractive();
            await PollQBitAsync();
        });
        DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedAsync());

        _ = PollQBitAsync();
    }

    public async Task PollQBitAsync()
    {
        if (Loading || !_owner.IsConnected) return;
        Loading = true;
        try
        {
            var torrents = await _owner.QBit.GetTorrentsAsync();

            Items.Clear();
            if (torrents.Count == 0)
            {
                Items.Add(new DownloadRow { AnimeName = "暂无下载任务", Episode = "--", Subgroup = "--", Quality = "--", Progress = 0, StatusText = "—" });
                return;
            }

            var sorted = torrents
                .OrderByDescending(t => t.Status == DownloadStatus.Downloading)
                .ThenBy(t => t.AnimeName)
                .ThenBy(t => t.Episode)
                .ToList();

            foreach (var t in sorted)
                Items.Add(new DownloadRow
                {
                    Hash = t.Hash,
                    AnimeName = t.AnimeName,
                    Episode = t.Episode > 0 ? t.Episode.ToString("D2") : "—",
                    Subgroup = t.Subgroup,
                    Quality = t.Quality,
                    Progress = t.Progress * 100,
                    StatusText = t.StatusText,
                    Status = t.Status
                });
        }
        catch (Exception ex)
        {
            Logger.Error("轮询下载状态失败", ex);
        }
        finally { Loading = false; }
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

public class DownloadRow
{
    public string Hash { get; set; } = "";
    public string AnimeName { get; set; } = "";
    public string Episode { get; set; } = "";
    public string Subgroup { get; set; } = "";
    public string Quality { get; set; } = "";
    public double Progress { get; set; }
    public string StatusText { get; set; } = "";
    public DownloadStatus Status { get; set; }
    public bool IsSelected { get; set; }
}
