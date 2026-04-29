using System.IO;
using System.Text.Json;
using AnimeSubscriber.Models;
using AnimeSubscriber.Services.Abstractions;

namespace AnimeSubscriber.Config;

public class AppConfig : IConfigService
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public QBitConfig QBittorrent { get; set; } = new();
    public DownloaderConfig Downloader { get; set; } = new();
    public SettingsConfig Settings { get; set; } = new();
    public List<Subscription> Subscriptions { get; set; } = new();
    public string ConfigPath { get; set; } = "";

    // ── Sync (kept for compatibility) ──────────────────────────

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var defaults = new AppConfig();
            defaults.Save(path);
            return defaults;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json, _options) ?? new AppConfig();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, _options);
        File.WriteAllText(path, json);
    }

    // ── Async ──────────────────────────────────────────────────

    public static async Task<AppConfig> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            var defaults = new AppConfig();
            await defaults.SaveAsync(path);
            return defaults;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous);
        return await JsonSerializer.DeserializeAsync<AppConfig>(stream, _options)
               ?? new AppConfig();
    }

    public async Task SaveAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, this, _options);
    }
}

public class QBitConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "Please Enter Password";
    public string SavePath { get; set; } = "Choose Your Savepath";
    public string Category { get; set; } = "anime";
}

public class DownloaderConfig
{
    public int ConcurrencyLimit { get; set; } = 3;
    public bool PreferCHS { get; set; } = true;
    public bool PreferMKV { get; set; } = true;
    public bool PreferHEVC { get; set; } = true;
}

public class SettingsConfig
{
    public int RssIntervalMinutes { get; set; } = 30;
    public string Proxy { get; set; } = "";
}
