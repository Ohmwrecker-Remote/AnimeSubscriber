using System.Windows.Controls;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber.Views;

public partial class LogsPage : UserControl
{
    public LogsPage()
    {
        InitializeComponent();
        DataContext = new LogsViewModel();
    }
}
