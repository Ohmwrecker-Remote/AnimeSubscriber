using AnimeSubscriber.Models;

namespace AnimeSubscriber.Services;

public static class Ranker
{
    public static (RssItem Item, ParsedTitle Parsed) PickBest(
        List<(RssItem Item, ParsedTitle Parsed)> entries)
    {
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

        if (t.Contains("chs") || t.Contains("ch_s") || t.Contains("简中") || t.Contains("简体"))
            score += 10;

        if (t.Contains(".mkv"))
            score += 5;

        if (t.Contains("hevc") || t.Contains("x265") || t.Contains("h265"))
            score += 3;

        return score;
    }
}
