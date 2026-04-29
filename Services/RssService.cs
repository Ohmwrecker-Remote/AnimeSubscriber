using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using AnimeSubscriber.Models;

namespace AnimeSubscriber.Services;

public class RssService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string? _proxy;
    private readonly string? _curlPath;

    public RssService(string? proxy = null)
    {
        _proxy = proxy;

        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        if (!string.IsNullOrEmpty(proxy))
        {
            handler.Proxy = new System.Net.WebProxy(proxy);
            handler.UseProxy = true;
            Logger.Info($"RSS 服务初始化: 代理={proxy}");
        }
        else
        {
            handler.UseProxy = false;
            Logger.Info("RSS 服务初始化: 直连模式");
        }

        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        _curlPath = FindCurl();
        if (!string.IsNullOrEmpty(_curlPath))
            Logger.Info($"curl 已就绪: {_curlPath}");
        else
            Logger.Warn("curl 未找到，将仅使用 HttpClient");
    }

    public async Task<List<RssItem>> FetchAsync(string rssUrl)
    {
        string xml;

        try
        {
            xml = await _httpClient.GetStringAsync(rssUrl);
            Logger.Info($"RSS 抓取成功 (HttpClient): {rssUrl[..Math.Min(80, rssUrl.Length)]}, 大小={xml.Length}");
        }
        catch (Exception ex1)
        {
            Logger.Warn($"RSS 抓取失败 (HttpClient): {ex1.Message}");
            if (!string.IsNullOrEmpty(_curlPath))
            {
                try
                {
                    xml = await FetchWithCurlAsync(rssUrl);
                    Logger.Info($"RSS 抓取成功 (curl): 大小={xml.Length}");
                }
                catch (Exception ex2)
                {
                    Logger.Error($"RSS 抓取失败 (curl): {ex2.Message}");
                    throw;
                }
            }
            else
            {
                Logger.Error($"RSS 抓取失败，curl 不可用: {ex1.Message}");
                throw;
            }
        }

        var items = ParseRss(xml);
        Logger.Info($"RSS 解析完成: {items.Count} 条");
        return items;
    }

    private async Task<string> FetchWithCurlAsync(string url)
    {
        var args = $"-s --max-time 15";
        if (!string.IsNullOrEmpty(_proxy))
            args += $" --proxy {_proxy}";
        args += $" \"{url}\"";

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = _curlPath!,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var proc = System.Diagnostics.Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync();
            var msg = $"curl 失败 (code={proc.ExitCode}): {err[..Math.Min(200, err.Length)]}";
            Logger.Error(msg);
            throw new Exception(msg);
        }

        Logger.Info($"curl 执行成功, 输出大小={output.Length}");
        return output;
    }

    private static List<RssItem> ParseRss(string xml)
    {
        var items = new List<RssItem>();
        var doc = XDocument.Parse(xml);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        foreach (var el in doc.Descendants(ns + "item"))
        {
            var item = new RssItem
            {
                Title = el.Element(ns + "title")?.Value ?? "",
                Guid = el.Element(ns + "guid")?.Value ?? "",
            };

            var enclosure = el.Element(ns + "enclosure");
            if (enclosure != null)
                item.TorrentUrl = enclosure.Attribute("url")?.Value ?? "";

            if (string.IsNullOrEmpty(item.TorrentUrl))
            {
                var linkEl = el.Element(ns + "link");
                if (linkEl != null)
                    item.TorrentUrl = linkEl.Value;
            }

            if (!string.IsNullOrEmpty(item.Title))
                items.Add(item);
        }

        return items;
    }

    private static string? FindCurl()
    {
        var paths = new[]
        {
            @"C:\Windows\System32\curl.exe",
            @"C:\Windows\SysWOW64\curl.exe",
        };
        return paths.FirstOrDefault(File.Exists);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
