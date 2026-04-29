using AnimeSubscriber.Models;
using AnimeSubscriber.Services.Abstractions;

namespace AnimeSubscriber.Services;

public class Matcher : IMatcher
{
    public static readonly Matcher Instance = new();

    public static bool Match(Subscription sub, ParsedTitle parsed) => Instance.MatchImpl(sub, parsed);

    bool IMatcher.Match(Subscription sub, ParsedTitle parsed) => MatchImpl(sub, parsed);

    private bool MatchImpl(Subscription sub, ParsedTitle parsed)
    {
        if (sub.IncludeGroups.Count > 0)
        {
            var matched = sub.IncludeGroups.Any(g =>
                parsed.Subgroup.Contains(g, StringComparison.OrdinalIgnoreCase));
            if (!matched) return false;
        }

        if (sub.ExcludeGroups.Count > 0)
        {
            var blocked = sub.ExcludeGroups.Any(g =>
                parsed.Subgroup.Contains(g, StringComparison.OrdinalIgnoreCase));
            if (blocked) return false;
        }

        if (!string.IsNullOrEmpty(sub.Quality))
        {
            if (!string.IsNullOrEmpty(parsed.Quality) && parsed.Quality != sub.Quality)
                return false;
        }

        if (parsed.IsBatch) return false;

        return true;
    }
}
