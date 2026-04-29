namespace AnimeSubscriber.Models;

public class RssItem
{
    public string Title { get; set; } = "";
    public string TorrentUrl { get; set; } = "";
    public string Guid { get; set; } = "";
    public DateTime PublishDate { get; set; } = DateTime.MinValue;
}
