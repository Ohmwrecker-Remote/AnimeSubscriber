using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Xml;
using AnimeSubscriber.Models;
using AnimeSubscriber.Services.Abstractions;

namespace AnimeSubscriber.Services;

public class RssService : IRssService
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

    public async Task<List<RssItem>> FetchAsync(string rssUrl, CancellationToken ct = default)
    {
        string xml;

        try
        {
            xml = await _httpClient.GetStringAsync(rssUrl, ct);
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

        using var sr = new StringReader(xml);
        using var reader = System.Xml.XmlReader.Create(sr, new System.Xml.XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = System.Xml.DtdProcessing.Ignore
        });

        var ns = string.Empty;
        string? currentElement = null;
        RssItem? currentItem = null;

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case System.Xml.XmlNodeType.Element:
                    if (reader.LocalName == "item")
                    {
                        currentItem = new RssItem();
                    }
                    else if (currentItem != null)
                    {
                        currentElement = reader.LocalName;
                    }
                    else if (reader.LocalName == "rss" || reader.LocalName == "channel")
                    {
                        ns = reader.NamespaceURI;
                    }

                    // enclosure: extract url attribute
                    if (currentItem != null && reader.LocalName == "enclosure")
                    {
                        var url = reader.GetAttribute("url");
                        if (!string.IsNullOrEmpty(url))
                            currentItem.TorrentUrl = url;
                    }
                    break;

                case System.Xml.XmlNodeType.Text:
                    if (currentItem != null && currentElement != null)
                    {
                        var value = reader.Value;
                        switch (currentElement)
                        {
                            case "title": currentItem.Title = value; break;
                            case "guid": currentItem.Guid = value; break;
                            case "link":
                                if (string.IsNullOrEmpty(currentItem.TorrentUrl))
                                    currentItem.TorrentUrl = value;
                                break;
                        }
                    }
                    break;

                case System.Xml.XmlNodeType.EndElement:
                    if (reader.LocalName == "item" && currentItem != null)
                    {
                        if (!string.IsNullOrEmpty(currentItem.Title))
                            items.Add(currentItem);
                        currentItem = null;
                    }
                    currentElement = null;
                    break;
            }
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
