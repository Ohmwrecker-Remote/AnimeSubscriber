namespace AnimeSubscriber.Models;

public class Subscription
{
    public string Name { get; set; } = "";
    public string RssUrl { get; set; } = "";
    public List<string> IncludeGroups { get; set; } = new();
    public List<string> ExcludeGroups { get; set; } = new();
    public string Quality { get; set; } = "";
    public string SaveSubfolder { get; set; } = "";
    public string EpisodePattern { get; set; } = "";
    public int EpisodeOffset { get; set; } = 0;
    public DateTime? LastCheckTime { get; set; }
}
