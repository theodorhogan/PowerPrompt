using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerPrompt.Common;
using PowerPrompt.Models;

namespace PowerPrompt.Rewrite;

/// <summary>
/// Small flyout shown directly above the tray icon offering the 3 rewrite options.
/// Navigable by mouse click and by arrow keys + Enter; Esc cancels. Closes on
/// selection, cancel, or losing focus. If the clipboard is empty it shows a brief
/// note instead of the options and does not proceed.
/// </summary>
public partial class RewritePopup : Window
{
    private readonly bool _optionsEnabled;
    private bool _closed;

    /// <summary>Raised once with the chosen option. Not raised on cancel.</summary>
    public event Action<RewriteOption>? OptionChosen;

    /// <param name="unavailableNote">
    /// When non-null, the popup shows this note (e.g. "Clipboard is empty" or an
    /// oversize warning) instead of the options and does not proceed.
    /// </param>
    public RewritePopup(string? unavailableNote = null)
    {
        InitializeComponent();
        _optionsEnabled = unavailableNote is null;

        if (_optionsEnabled)
        {
            OptionsList.ItemsSource = RewriteOptionInfo.All
                .Select(RewriteOptionInfo.DisplayName)
                .ToList();
            OptionsList.Visibility = Visibility.Visible;
            NoteText.Visibility = Visibility.Collapsed;
        }
        else
        {
            OptionsList.Visibility = Visibility.Collapsed;
            NoteText.Text = unavailableNote;
            NoteText.Visibility = Visibility.Visible;
        }

        Loaded += OnLoaded;
        Deactivated += (_, _) => CloseOnce();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionAboveTray();
        WindowActivation.BringToForeground(this);

        if (_optionsEnabled)
        {
            OptionsList.SelectedIndex = 0;
            // Move keyboard focus onto the first item so arrow keys work immediately.
            if (OptionsList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem item)
                item.Focus();
            else
                OptionsList.Focus();
        }
        else
        {
            Focus();
        }
    }

    private void PositionAboveTray()
    {
        // Work area excludes the taskbar, so its bottom-right corner sits directly
        // above the tray on a standard Windows layout. Values are device-independent.
        var wa = SystemParameters.WorkArea;
        const double margin = 8;
        Left = wa.Right - ActualWidth - margin;
        Top = wa.Bottom - ActualHeight - margin;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseOnce();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _optionsEnabled)
        {
            Confirm(OptionsList.SelectedIndex);
            e.Handled = true;
        }
    }

    private void OptionsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is not null)
            Confirm(OptionsList.ItemContainerGenerator.IndexFromContainer(item));
    }

    private void Confirm(int index)
    {
        if (_closed || index < 0 || index >= RewriteOptionInfo.All.Length)
            return;

        var option = RewriteOptionInfo.All[index];
        // Set the guard before closing so the Deactivated handler does not re-enter
        // Close() (which would throw and skip delivering the choice). Deliver first,
        // then close.
        _closed = true;
        OptionChosen?.Invoke(option);
        Close();
    }

    /// <summary>Idempotent close — guards against re-entrant Close() during teardown.</summary>
    private void CloseOnce()
    {
        if (_closed)
            return;
        _closed = true;
        Close();
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
