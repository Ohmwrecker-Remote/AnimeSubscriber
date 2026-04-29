using System.IO;
using System.Threading.Channels;

namespace AnimeSubscriber.Services;

public static class Logger
{
    private static readonly string LogDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static readonly string LogFile =
        Path.Combine(LogDir, "log.txt");

    private static readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    private static readonly CancellationTokenSource _cts = new();
    private static readonly Task _writerTask;
    private static StreamWriter? _writer;
    private static bool _initialized;

    static Logger()
    {
        _writerTask = Task.Run(WriteLoopAsync);
    }

    private static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            Directory.CreateDirectory(LogDir);

            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 5 * 1024 * 1024)
            {
                var backup = Path.Combine(LogDir, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.Move(LogFile, backup);
            }

            _writer = new StreamWriter(LogFile, append: true) { AutoFlush = false };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logger init failed: {ex.Message}");
        }
    }

    private static async Task WriteLoopAsync()
    {
        try
        {
            await Task.Yield(); // let static ctor finish
            Init();

            if (_writer == null) return;

            var reader = _channel.Reader;
            var count = 0;
            var lastFlush = DateTime.UtcNow;
            await foreach (var entry in reader.ReadAllAsync(_cts.Token))
            {
                await _writer.WriteLineAsync(entry);
                count++;
                if (count >= 50 || (DateTime.UtcNow - lastFlush).TotalSeconds >= 5)
                {
                    await _writer.FlushAsync();
                    count = 0;
                    lastFlush = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logger write loop error: {ex.Message}");
        }
        finally
        {
            try { _writer?.Dispose(); } catch { }
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message} | {ex.GetType().Name}: {ex.Message}" : message;
        Write("ERROR", msg);
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        _channel.Writer.TryWrite(line);
    }

    public static async Task FlushAndStopAsync()
    {
        _channel.Writer.Complete();
        try { await _writerTask.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException)
        {
            System.Diagnostics.Debug.WriteLine("Logger flush timed out after 3s");
        }
    }

    public static string GetLogPath() => LogFile;
}
