using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AnimeSubscriber.Services;

namespace AnimeSubscriber.ViewModels;

public class LogsViewModel : BaseViewModel
{
    private string _logText = "";
    public string LogText
    {
        get => _logText;
        set => Set(ref _logText, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenFileCommand { get; }

    public LogsViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshLog);
        OpenFileCommand = new RelayCommand(OpenLogFile);
        RefreshLog();
    }

    public void RefreshLog()
    {
        try
        {
            var path = Logger.GetLogPath();
            if (!File.Exists(path))
            {
                LogText = "日志文件尚未生成或为空。操作应用后将自动生成日志。";
                return;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            const int tailBytes = 64 * 1024;
            string content;

            if (fs.Length <= tailBytes)
            {
                using var sr = new StreamReader(fs);
                content = sr.ReadToEnd();
            }
            else
            {
                fs.Seek(-tailBytes, SeekOrigin.End);
                using var sr = new StreamReader(fs);
                content = sr.ReadToEnd();
                // Skip partial first line if we started mid-line
                var firstNewline = content.IndexOf('\n');
                if (firstNewline > 0)
                    content = content[(firstNewline + 1)..];
            }

            if (content.Length > 0)
            {
                var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.TrimEnd('\r'))
                    .ToArray();
                LogText = string.Join(Environment.NewLine, lines.Reverse().Take(500));
                return;
            }

            LogText = "日志文件为空。操作应用后将自动生成日志。";
        }
        catch
        {
            LogText = "无法读取日志文件。";
        }
    }

    private static void OpenLogFile()
    {
        var path = Logger.GetLogPath();
        if (File.Exists(path))
            Process.Start("notepad.exe", path);
        else
            System.Windows.MessageBox.Show("日志文件尚未生成", "提示");
    }
}
