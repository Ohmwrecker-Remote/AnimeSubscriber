namespace AnimeSubscriber.Models;

public class ParsedTitle
{
    public string Subgroup { get; set; } = "";
    public string AnimeName { get; set; } = "";
    public int? Episode { get; set; }
    public string Quality { get; set; } = "";
    public bool IsBatch { get; set; }
    public bool IsEnd { get; set; }
}
