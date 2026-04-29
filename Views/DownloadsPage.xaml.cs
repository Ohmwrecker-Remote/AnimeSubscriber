using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber.Views;

public partial class DownloadsPage : UserControl
{
    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage()
    {
        InitializeComponent();
        var mainVm = ((App)Application.Current).MainViewModel;
        ViewModel = new DownloadsViewModel(mainVm);
        DataContext = ViewModel;
    }

    private void OnDataGridMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;

        var hit = dg.InputHitTest(e.GetPosition(dg));
        var cell = FindParentDataGridCell(hit as DependencyObject);
        if (cell == null) return;

        var col = cell.Column;
        if (col?.DisplayIndex != 0) return;

        if (cell.DataContext is DownloadRow row && !string.IsNullOrEmpty(row.Hash))
            row.IsSelected = !row.IsSelected;
    }

    private static DataGridCell? FindParentDataGridCell(DependencyObject? child)
    {
        while (child != null)
        {
            if (child is DataGridCell cell) return cell;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
