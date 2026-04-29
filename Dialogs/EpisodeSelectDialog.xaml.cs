using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AnimeSubscriber.Dialogs;

public partial class EpisodeSelectDialog : Window
{
    private readonly List<SelectableEntry> _allEntries;
    public List<EpisodeEntry> SelectedEntries { get; } = new();

    public EpisodeSelectDialog(List<EpisodeEntry> entries)
    {
        InitializeComponent();

        _allEntries = entries.Select(e => new SelectableEntry(e)).ToList();
        foreach (var se in _allEntries)
            se.PropertyChanged += (_, _) => UpdateCount();

        EpisodeList.ItemsSource = new ObservableCollection<SelectableEntry>(_allEntries);

        // Select all by default
        foreach (var se in _allEntries)
            se.IsSelected = true;

        UpdateCount();
    }

    private void UpdateCount()
    {
        var count = _allEntries.Count(e => e.IsSelected);
        CountLabel.Text = $"共 {_allEntries.Count} 集  ·  已选 {count} 集";
        ConfirmBtn.Content = $"确认下载 ({count})";
        ConfirmBtn.IsEnabled = count > 0;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var se in _allEntries) se.IsSelected = true;
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var se in _allEntries) se.IsSelected = false;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        SelectedEntries.Clear();
        foreach (var se in _allEntries)
            if (se.IsSelected)
                SelectedEntries.Add(se.Entry);

        if (SelectedEntries.Count == 0)
        {
            MessageBox.Show("请至少选择一集", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}

/// <summary>Wrapper that adds IsSelected for checkbox binding.</summary>
public class SelectableEntry : INotifyPropertyChanged
{
    public EpisodeEntry Entry { get; }
    public string Title => Truncate(Entry.Title, 50);
    public int Episode => Entry.Episode;
    public string Quality => Entry.Quality;
    public string Subgroup => Entry.Subgroup;

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public SelectableEntry(EpisodeEntry entry) => Entry = entry;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 1)] + "…";
}
