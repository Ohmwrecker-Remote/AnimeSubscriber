using System.Drawing;

namespace AnimeSubscriber.Services;

public static class NotificationService
{
    private static System.Windows.Forms.NotifyIcon? _icon;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _icon = new System.Windows.Forms.NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "AnimeSubscriber"
            };
        }
        catch { }
    }

    public static void Show(string title, string message)
    {
        if (_icon == null) return;
        try
        {
            _icon.ShowBalloonTip(5000, title, message, System.Windows.Forms.ToolTipIcon.Info);
        }
        catch { }
    }

    public static void Dispose()
    {
        try
        {
            _icon?.Dispose();
            _icon = null;
        }
        catch { }
    }
}
