using AnimeSubscriber.Models;

namespace AnimeSubscriber.Services;

public static class Matcher
{
    public static bool Match(Subscription sub, ParsedTitle parsed)
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
