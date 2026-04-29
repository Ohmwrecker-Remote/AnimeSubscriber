using System.Windows.Controls;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        var mainVm = ((App)Application.Current).MainViewModel;
        DataContext = new SettingsViewModel(mainVm);
    }
}
