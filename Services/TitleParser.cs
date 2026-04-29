using System.Text.RegularExpressions;
using AnimeSubscriber.Models;

namespace AnimeSubscriber.Services;

public static partial class TitleParser
{
    [GeneratedRegex(@"【(?<subgroup>[^】]+)】\s*\[(?<name>.+?)\]\s*\[(?<ep>\d+)\]")]
    private static partial Regex CBracketPattern();

    [GeneratedRegex(@"\[(?<subgroup>[^\[\]]+?)\]\s*\[(?<name>.+?)\]\s*\[(?<ep>\d+)\]")]
    private static partial Regex BracketBracketPattern();

    [GeneratedRegex(@"\[(?<subgroup>[^\[\]]+?)\]\s*(?<name>.+?)\s*-\s*(?<ep>\d+)\s*")]
    private static partial Regex DashPattern();

    [GeneratedRegex(@"\[(?<subgroup>[^\[\]]+?)\]\s*(?<name>.+?)/.+?\s*-\s*(?<ep>\d+)\s*")]
    private static partial Regex DashWithAltPattern();

    [GeneratedRegex(@"\[(?<subgroup>[^\[\]]+?)\]\s*(?<name>.+?)\s*第\s*(?<ep>\d+)\s*[话集]")]
    private static partial Regex ChineseEpPattern();

    [GeneratedRegex(@"\[(?<subgroup>[^\[\]]+?)\]\s*(?<name>.+?)\s*-\s*(?<ep>\d+)\.\w+$")]
    private static partial Regex FileExtPattern();

    private static readonly Regex[] Patterns =
    {
        CBracketPattern(), BracketBracketPattern(),
        DashPattern(), DashWithAltPattern(),
        ChineseEpPattern(), FileExtPattern()
    };

    private static readonly Regex EpInBracketPattern =
        new(@"\[(?<ep>\d{2,3})\]", RegexOptions.Compiled);

    private static readonly Regex SubgroupPattern =
        new(@"[\[【](?<subgroup>[^\[\]【】]+?)[\]】]", RegexOptions.Compiled);

    public static ParsedTitle? Parse(string title)
    {
        if (string.IsNullOrEmpty(title))
            return null;

        foreach (var pattern in Patterns)
        {
            var m = pattern.Match(title);
            if (!m.Success) continue;

            var subgroup = m.Groups["subgroup"].Value.Trim();
            var name = m.Groups["name"].Value.Trim();
            var episode = int.Parse(m.Groups["ep"].Value);

            return Build(subgroup, name, episode, title);
        }

        var fbEp = EpInBracketPattern.Match(title);
        if (!fbEp.Success)
        {
            Logger.Warn($"TitleParser 解析失败: {title[..Math.Min(80, title.Length)]}");
            return null;
        }

        var fbEpisode = int.Parse(fbEp.Groups["ep"].Value);
        var fbName = title[..fbEp.Index].TrimEnd('[', ']', ' ', '-', '/');

        var fbSubgroup = SubgroupPattern.Match(title);
        var fbSub = fbSubgroup.Success ? fbSubgroup.Groups["subgroup"].Value.Trim() : "";

        if (fbSub.Length > 0 && fbName.Contains(fbSub))
        {
            fbName = fbName.Replace(fbSub, "").Trim('[', ']', '【', '】', ' ');
        }

        Logger.Info($"TitleParser 兜底匹配: [{fbSub}] {fbName} - {fbEpisode}");
        return Build(fbSub, fbName, fbEpisode, title);
    }

    private static ParsedTitle Build(string subgroup, string name, int episode, string title)
    {
        var quality = "";
        if (title.Contains("2160p") || title.Contains("4K"))
            quality = "2160p";
        else if (title.Contains("1080p") || title.Contains("1080P"))
            quality = "1080p";
        else if (title.Contains("720p") || title.Contains("720P"))
            quality = "720p";

        return new ParsedTitle
        {
            Subgroup = subgroup,
            AnimeName = name,
            Episode = episode,
            Quality = quality,
            IsBatch = title.Replace(" ", "").Contains("[01-") || title.Contains("合集"),
            IsEnd = title.Contains("END") || title.Contains("Fin]")
        };
    }
}
