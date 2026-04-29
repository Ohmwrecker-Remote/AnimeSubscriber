using System.IO;
using AnimeSubscriber.Models;
using AnimeSubscriber.Services.Abstractions;

namespace AnimeSubscriber.Services;

public class FileScanner : IFileScanner
{
    public static readonly FileScanner Instance = new();

    public static bool EpisodeExists(string savePath, int episode) =>
        Instance.EpisodeExistsImpl(savePath, episode);

    public static DownloadStatus GetEpisodeStatus(string savePath, int episode) =>
        Instance.GetEpisodeStatusImpl(savePath, episode);

    bool IFileScanner.EpisodeExists(string savePath, int episode) =>
        EpisodeExistsImpl(savePath, episode);

    DownloadStatus IFileScanner.GetEpisodeStatus(string savePath, int episode) =>
        GetEpisodeStatusImpl(savePath, episode);

    private bool EpisodeExistsImpl(string savePath, int episode)
    {
        if (!Directory.Exists(savePath))
            return false;

        var epPatterns = new[]
        {
            $"{episode:D2}", $"{episode:D3}",
            $"第{episode}话", $"第{episode}集",
            $"E{episode:D2}", $"S01E{episode:D2}"
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(savePath))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (epPatterns.Any(p => name.Contains(p)))
                    return true;
            }

            foreach (var dir in Directory.EnumerateDirectories(savePath))
            {
                var name = Path.GetFileName(dir);
                if (epPatterns.Any(p => name.Contains(p)))
                    return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"文件扫描失败: {savePath} E{episode:D2} | {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    private DownloadStatus GetEpisodeStatusImpl(string savePath, int episode)
    {
        if (!Directory.Exists(savePath))
            return DownloadStatus.Waiting;

        if (EpisodeExistsImpl(savePath, episode))
            return DownloadStatus.Completed;

        try
        {
            var partFiles = Directory.EnumerateFiles(savePath, "*.part", SearchOption.AllDirectories);
            var epStr = $"{episode:D2}";
            if (partFiles.Any(f => f.Contains(epStr)))
                return DownloadStatus.Downloading;
        }
        catch (Exception ex)
        {
            Logger.Warn($"部分下载检测失败: {savePath} E{episode:D2} | {ex.GetType().Name}: {ex.Message}");
        }

        return DownloadStatus.Waiting;
    }
}
