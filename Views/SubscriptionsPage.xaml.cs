using System.Windows.Controls;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber.Views;

public partial class SubscriptionsPage : UserControl
{
    public SubscriptionsViewModel ViewModel { get; }

    public SubscriptionsPage()
    {
        InitializeComponent();
        var mainVm = ((App)Application.Current).MainViewModel;
        ViewModel = new SubscriptionsViewModel(mainVm);
        DataContext = ViewModel;
        ViewModel.RefreshList();
    }
}
