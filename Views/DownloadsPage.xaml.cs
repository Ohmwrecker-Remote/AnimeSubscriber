using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber.Views;

public partial class DownloadsPage : UserControl
{
    public DownloadsViewModel ViewModel { get; }
    private string? _selectedHash;

    public DownloadsPage()
    {
        InitializeComponent();
        var mainVm = ((App)Application.Current).MainViewModel;
        ViewModel = new DownloadsViewModel(mainVm);
        DataContext = ViewModel;
        Unloaded += OnUnloaded;

        DownloadGrid.SelectionChanged += (_, _) =>
        {
            if (DownloadGrid.SelectedItem is DownloadRow r && !string.IsNullOrEmpty(r.Hash))
                _selectedHash = r.Hash;
        };

        ViewModel.Items.CollectionChanged += (_, _) =>
        {
            if (_selectedHash == null) return;
            var match = ViewModel.Items.FirstOrDefault(r => r.Hash == _selectedHash);
            if (match != null) DownloadGrid.SelectedItem = match;
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StopDownloadPolling();
    }
}
