namespace AnimeSubscriber.Models;

public enum DownloadStatus
{
    Waiting,
    Downloading,
    Completed,
    Failed
}

public class DownloadEntry
{
    public string Hash { get; set; } = "";
    public string AnimeName { get; set; } = "";
    public int Episode { get; set; }
    public string Subgroup { get; set; } = "";
    public string Quality { get; set; } = "";
    public DownloadStatus Status { get; set; } = DownloadStatus.Waiting;
    public double Progress { get; set; }
    public string FilePath { get; set; } = "";

    public string Display => $"{AnimeName} 第{Episode:D2}话 [{Subgroup}] {Quality}";

    public string StatusText => Status switch
    {
        DownloadStatus.Completed => "✅ 已下载",
        DownloadStatus.Downloading => $"⬇ 下载中 {Progress:P0}",
        DownloadStatus.Waiting => "⏳ 等待中",
        DownloadStatus.Failed => "❌ 失败",
        _ => ""
    };
}
