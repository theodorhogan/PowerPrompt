using System.Windows;
using PowerPrompt.Common;

namespace PowerPrompt.Views.Dialogs;

/// <summary>Picks a single category from a combo. Used to move a prompt.</summary>
public partial class ChooseCategoryDialog : Window
{
    public string? Selected => CategoryCombo.SelectedItem as string;

    private ChooseCategoryDialog(string title, string label, IEnumerable<string> categories)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = label;
        CategoryCombo.ItemsSource = categories.ToList();
        if (CategoryCombo.Items.Count > 0)
            CategoryCombo.SelectedIndex = 0;
        SourceInitialized += (_, _) => WindowTheming.ApplyDarkTitleBar(this);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
            return;
        DialogResult = true;
    }

    public static bool TryChoose(Window owner, string title, string label, IEnumerable<string> categories, out string selected)
    {
        var dialog = new ChooseCategoryDialog(title, label, categories) { Owner = owner };
        if (dialog.ShowDialog() == true && dialog.Selected is not null)
        {
            selected = dialog.Selected;
            return true;
        }
        selected = string.Empty;
        return false;
    }
}
