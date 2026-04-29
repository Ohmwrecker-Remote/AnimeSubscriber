using System.Windows.Controls;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber.Views;

public partial class SettingsPage : UserControl
{
    private SettingsViewModel _vm = null!;

    public SettingsPage()
    {
        InitializeComponent();
        var mainVm = ((App)Application.Current).MainViewModel;
        _vm = new SettingsViewModel(mainVm);
        DataContext = _vm;

        // Load initial password into PasswordBox (not bindable)
        QbPasswordBox.Password = _vm.QbPass;
    }

    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        _vm.QbPass = QbPasswordBox.Password;
    }
}
