using AnimeSubscriber.Models;
using AnimeSubscriber.Services.Abstractions;

namespace AnimeSubscriber.Services;

public class Ranker : IRanker
{
    public static readonly Ranker Instance = new();

    // Configurable via App.xaml.cs DI setup
    public bool PreferCHS { get; set; } = true;
    public bool PreferMKV { get; set; } = true;
    public bool PreferHEVC { get; set; } = true;

    public static (RssItem Item, ParsedTitle Parsed)? PickBest(
        List<(RssItem Item, ParsedTitle Parsed)> entries) => Instance.PickBestImpl(entries);

    (RssItem, ParsedTitle)? IRanker.PickBest(List<(RssItem, ParsedTitle)> entries) => PickBestImpl(entries);

    private (RssItem Item, ParsedTitle Parsed)? PickBestImpl(List<(RssItem Item, ParsedTitle Parsed)> entries)
    {
        if (entries.Count == 0)
            return null;
        if (entries.Count == 1)
            return entries[0];

        return entries
            .OrderByDescending(e => Score(e.Item.Title, e.Parsed))
            .ThenBy(e => entries.IndexOf(e))
            .First();
    }

    private static int Score(string title, ParsedTitle parsed)
    {
        var score = 0;

        var t = title.ToLowerInvariant();

        score += parsed.Quality switch
        {
            "2160p" => 40,
            "1080p" => 30,
            "720p" => 20,
            _ => 0
        };

        if (Instance.PreferCHS &&
            (t.Contains("chs") || t.Contains("ch_s") || t.Contains("简中") || t.Contains("简体")))
            score += 10;

        if (Instance.PreferMKV && t.Contains(".mkv"))
            score += 5;

        if (Instance.PreferHEVC &&
            (t.Contains("hevc") || t.Contains("x265") || t.Contains("h265")))
            score += 3;

        return score;
    }
}
