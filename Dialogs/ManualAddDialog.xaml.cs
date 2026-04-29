using System.Windows;

namespace AnimeSubscriber.Dialogs;

public partial class ManualAddDialog : Window
{
    public string SubName => NameBox.Text;
    public string RssUrl => "";

    public ManualAddDialog(string title, string defaultName = "")
    {
        InitializeComponent();
        Title = title;
        NameBox.Text = defaultName;
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
