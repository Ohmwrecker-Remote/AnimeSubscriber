using System.Windows;
using AnimeSubscriber.ViewModels;

namespace AnimeSubscriber;

public partial class App : Application
{
    public MainViewModel MainViewModel => (MainViewModel)MainWindow.DataContext;
}
