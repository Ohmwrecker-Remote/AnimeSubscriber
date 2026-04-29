namespace AnimeSubscriber.Dialogs;

public record EpisodeEntry(string Title, int Episode, string Quality, string Subgroup, string TorrentUrl, string SubscriptionName = "");
