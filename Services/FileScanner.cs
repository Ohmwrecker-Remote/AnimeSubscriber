using System.IO;
using AnimeSubscriber.Models;

namespace AnimeSubscriber.Services;

public static class FileScanner
{
    public static bool EpisodeExists(string savePath, int episode)
    {
        if (!Directory.Exists(savePath))
            return false;

        var epPatterns = new[]
        {
            $"{episode:D2}",
            $"{episode:D3}",
            $"第{episode}话",
            $"第{episode}集",
            $"E{episode:D2}",
            $"S01E{episode:D2}"
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
        catch
        {
            // 目录无法访问，返回 false
        }

        return false;
    }

    public static DownloadStatus GetEpisodeStatus(string savePath, int episode)
    {
        if (!Directory.Exists(savePath))
            return DownloadStatus.Waiting;

        if (EpisodeExists(savePath, episode))
            return DownloadStatus.Completed;

        try
        {
            var partFiles = Directory.EnumerateFiles(savePath, "*.part", SearchOption.AllDirectories);
            var epStr = $"{episode:D2}";
            if (partFiles.Any(f => f.Contains(epStr)))
                return DownloadStatus.Downloading;
        }
        catch { }

        return DownloadStatus.Waiting;
    }
}
