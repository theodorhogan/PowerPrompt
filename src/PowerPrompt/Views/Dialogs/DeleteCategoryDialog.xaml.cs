using System.Windows;
using PowerPrompt.Common;
using PowerPrompt.Library;

namespace PowerPrompt.Views.Dialogs;

/// <summary>
/// Safeguard for deleting a non-empty category: the user must choose to move its
/// prompts elsewhere or delete them with it — data is never silently lost.
/// </summary>
public partial class DeleteCategoryDialog : Window
{
    public CategoryDeleteMode Mode { get; private set; }
    public string? MoveTarget { get; private set; }

    private DeleteCategoryDialog(string category, int promptCount, IReadOnlyList<string> otherCategories)
    {
        InitializeComponent();
        MessageText.Text = $"Category \"{category}\" has {promptCount} prompt(s). What should happen to them?";

        if (otherCategories.Count > 0)
        {
            MoveTargetCombo.ItemsSource = otherCategories;
            MoveTargetCombo.SelectedIndex = 0;
            MoveOption.IsChecked = true;
        }
        else
        {
            // Nowhere to move them — only deletion is possible.
            MoveOption.IsEnabled = false;
            MoveTargetCombo.IsEnabled = false;
            DeleteOption.IsChecked = true;
        }
        SourceInitialized += (_, _) => WindowTheming.ApplyDarkTitleBar(this);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (MoveOption.IsChecked == true)
        {
            if (MoveTargetCombo.SelectedItem is not string target)
                return;
            Mode = CategoryDeleteMode.MovePrompts;
            MoveTarget = target;
        }
        else
        {
            Mode = CategoryDeleteMode.DeletePrompts;
            MoveTarget = null;
        }
        DialogResult = true;
    }

    /// <summary>Returns false if cancelled. On true, read Mode/MoveTarget.</summary>
    public static bool Ask(Window owner, string category, int promptCount, IReadOnlyList<string> otherCategories,
        out CategoryDeleteMode mode, out string? moveTarget)
    {
        var dialog = new DeleteCategoryDialog(category, promptCount, otherCategories) { Owner = owner };
        if (dialog.ShowDialog() == true)
        {
            mode = dialog.Mode;
            moveTarget = dialog.MoveTarget;
            return true;
        }
        mode = CategoryDeleteMode.DeletePrompts;
        moveTarget = null;
        return false;
    }
}
