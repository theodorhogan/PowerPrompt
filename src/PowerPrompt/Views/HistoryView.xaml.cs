using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PowerPrompt.Common;
using PowerPrompt.History;
using PowerPrompt.Models;

namespace PowerPrompt.Views;

/// <summary>
/// Feature A local history: reverse-chronological table; selecting a row expands
/// the full input/output; per-entry re-copy of the output; clear all.
/// </summary>
public partial class HistoryView : UserControl
{
    private readonly HistoryStore _history;
    private readonly DispatcherTimer _copiedTimer;
    private HistoryEntry? _selected;

    public HistoryView(HistoryStore history)
    {
        InitializeComponent();
        _history = history;

        _copiedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _copiedTimer.Tick += (_, _) =>
        {
            _copiedTimer.Stop();
            CopiedNote.Visibility = Visibility.Collapsed;
        };

        Loaded += (_, _) => Reload();
    }

    /// <summary>Refreshes the table from the shared store (called when the view is shown).</summary>
    public void Reload()
    {
        Grid.ItemsSource = _history.Entries.ToList();
        EmptyState.Visibility = _history.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Detail.Visibility = Visibility.Collapsed;
        _selected = null;
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = Grid.SelectedItem as HistoryEntry;
        if (_selected is null)
        {
            Detail.Visibility = Visibility.Collapsed;
            return;
        }

        InputBox.Text = _selected.Input;
        OutputBox.Text = _selected.Output;
        RecopyButton.IsEnabled = _selected.Ok && _selected.Output.Length > 0;
        CopiedNote.Visibility = Visibility.Collapsed;
        Detail.Visibility = Visibility.Visible;
    }

    private void Recopy_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _selected.Output.Length == 0)
            return;

        ClipboardHelper.SetText(_selected.Output);
        CopiedNote.Visibility = Visibility.Visible;
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Entries.Count == 0)
            return;

        if (MessageBox.Show("Clear all rewrite history?", "PowerPrompt",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _history.Clear();
            Reload();
        }
    }
}
