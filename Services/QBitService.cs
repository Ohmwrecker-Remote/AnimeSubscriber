using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeSubscriber.Models;
using AnimeSubscriber.Services.Abstractions;

namespace AnimeSubscriber.Services;

public class QBitService : IQBitService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private bool _loggedIn;

    public bool IsConnected => _loggedIn;

    public QBitService(string host = "localhost", int port = 8080,
                        string username = "admin", string password = "Please Enter Password")
    {
        _baseUrl = $"http://{host}:{port}";

        var handler = new HttpClientHandler
        {
            UseProxy = false
        };

        var cookies = new CookieContainer();
        handler.CookieContainer = cookies;

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        _host = host;
        _port = port;
        _username = username;
        _password = password;
    }

    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = _username,
                ["password"] = _password
            });

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            var resp = await _httpClient.PostAsync($"{_baseUrl}/api/v2/auth/login", content, timeoutCts.Token);
            var body = await resp.Content.ReadAsStringAsync(timeoutCts.Token);

            _loggedIn = body == "Ok.";
            if (_loggedIn)
                Logger.Info($"qBittorrent 登录成功: {_baseUrl}");
            else
                Logger.Warn($"qBittorrent 登录失败: {_baseUrl}, 响应={body}");
        }
        catch (Exception ex)
        {
            _loggedIn = false;
            Logger.Error($"qBittorrent 连接失败: {_baseUrl}", ex);
        }
    }

    public async Task<bool> AddTorrentAsync(string torrentUrl, string savePath, string category, int retryCount = 1, CancellationToken ct = default)
    {
        if (!_loggedIn) return false;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["urls"] = torrentUrl,
                ["savepath"] = savePath,
                ["category"] = category,
                ["autoTMM"] = "false",
                ["paused"] = "false"
            });

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v2/torrents/add")
            {
                Content = content
            };
            request.Headers.Referrer = new Uri(_baseUrl);

            var resp = await _httpClient.SendAsync(request, ct);
            var ok = resp.StatusCode == HttpStatusCode.OK;
            if (!ok)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                Logger.Warn($"添加种子失败 [{resp.StatusCode}]: {body[..Math.Min(200, body.Length)]} | url={torrentUrl[..Math.Min(80, torrentUrl.Length)]}");

                if (resp.StatusCode == HttpStatusCode.Forbidden && retryCount > 0)
                {
                    Logger.Info("尝试重新登录 qBittorrent...");
                    await ConnectAsync(ct);
                    if (_loggedIn)
                    {
                        Logger.Info("重新登录成功，重试添加种子");
                        return await AddTorrentAsync(torrentUrl, savePath, category, retryCount - 1, ct);
                    }
                }
            }
            return ok;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            Logger.Error($"添加种子异常: url={torrentUrl[..Math.Min(80, torrentUrl.Length)]}", ex);
            return false;
        }
    }

    public async Task<List<DownloadEntry>> GetTorrentsAsync(CancellationToken ct = default)
    {
        if (!_loggedIn) return new List<DownloadEntry>();

        try
        {
            var resp = await _httpClient.GetAsync(
                $"{_baseUrl}/api/v2/torrents/info?category=anime", ct);
            if (resp.StatusCode != HttpStatusCode.OK)
                return new List<DownloadEntry>();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var torrents = JsonSerializer.Deserialize<List<QBitTorrent>>(json);

            return torrents?.Select(t =>
            {
                var parsed = TitleParser.Parse(t.Name);

                return new DownloadEntry
                {
                    Hash = t.Hash,
                    AnimeName = parsed?.AnimeName ?? t.Name,
                    Episode = parsed?.Episode ?? 0,
                    Subgroup = parsed?.Subgroup ?? "",
                    Quality = parsed?.Quality ?? "",
                    Status = ParseTorrentStatus(t.State, t.Progress),
                    Progress = t.Progress,
                    FilePath = t.SavePath
                };
            }).ToList() ?? new List<DownloadEntry>();
        }
        catch (OperationCanceledException) { return new List<DownloadEntry>(); }
        catch (Exception ex)
        {
            Logger.Error("获取种子列表失败", ex);
            return new List<DownloadEntry>();
        }
    }

    public async Task<bool> DeleteTorrentsAsync(List<string> hashes, bool deleteFiles = true, int retryCount = 1, CancellationToken ct = default)
    {
        if (!_loggedIn || hashes.Count == 0) return false;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["hashes"] = string.Join("|", hashes),
                ["deleteFiles"] = deleteFiles ? "true" : "false"
            });

            var resp = await _httpClient.PostAsync(
                $"{_baseUrl}/api/v2/torrents/delete", content, ct);

            if (resp.StatusCode == HttpStatusCode.OK)
                return true;

            if (resp.StatusCode == HttpStatusCode.Forbidden && retryCount > 0)
            {
                Logger.Info("删除种子时鉴权失败，尝试重新登录...");
                await ConnectAsync(ct);
                if (_loggedIn)
                    return await DeleteTorrentsAsync(hashes, deleteFiles, retryCount - 1, ct);
            }

            Logger.Warn($"删除种子失败 [{resp.StatusCode}]");
            return false;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            Logger.Error($"删除种子异常: hashes={string.Join("|", hashes)}", ex);
            return false;
        }
    }

    private static DownloadStatus ParseTorrentStatus(string? state, double progress)
    {
        return state switch
        {
            "uploading" or "pausedUP" or "stalledUP" or "checkingUP" => DownloadStatus.Completed,
            "downloading" or "stalledDL" or "checkingDL" or "forcedDL" => DownloadStatus.Downloading,
            "error" or "missingFiles" => DownloadStatus.Failed,
            _ => DownloadStatus.Waiting
        };
    }

    private class QBitTorrent
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("progress")]
        public double Progress { get; set; }

        [JsonPropertyName("save_path")]
        public string SavePath { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
