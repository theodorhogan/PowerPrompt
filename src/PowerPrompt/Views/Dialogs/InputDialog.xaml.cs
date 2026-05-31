using System.Windows;
using PowerPrompt.Common;

namespace PowerPrompt.Views.Dialogs;

/// <summary>
/// Generic input dialog: a single-line Name field plus an optional multiline Body
/// field. Used for new/rename category (name only) and new/edit prompt (name + body).
/// </summary>
public partial class InputDialog : Window
{
    public string NameValue => NameBox.Text.Trim();
    public string BodyValue => BodyBox.Text;

    private InputDialog(string title, string nameLabel, string nameValue, bool withBody, string bodyLabel, string bodyValue)
    {
        InitializeComponent();
        Title = title;
        NameLabel.Text = nameLabel;
        NameBox.Text = nameValue;

        if (withBody)
        {
            BodyLabel.Text = bodyLabel;
            BodyBox.Text = bodyValue;
        }
        else
        {
            BodyLabel.Visibility = Visibility.Collapsed;
            BodyBox.Visibility = Visibility.Collapsed;
        }

        SourceInitialized += (_, _) => WindowTheming.ApplyDarkTitleBar(this);
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
            return; // name is required
        DialogResult = true;
    }

    /// <summary>Single-line prompt (category name, rename). Returns false if cancelled.</summary>
    public static bool TryGetName(Window owner, string title, string label, string initial, out string value)
    {
        var dialog = new InputDialog(title, label, initial, withBody: false, string.Empty, string.Empty) { Owner = owner };
        if (dialog.ShowDialog() == true)
        {
            value = dialog.NameValue;
            return true;
        }
        value = string.Empty;
        return false;
    }

    /// <summary>Name + body editor (new/edit prompt). Returns false if cancelled.</summary>
    public static bool TryEditPrompt(Window owner, string title, string name, string body, out string outName, out string outBody)
    {
        var dialog = new InputDialog(title, "Name", name, withBody: true, "Body", body) { Owner = owner };
        if (dialog.ShowDialog() == true)
        {
            outName = dialog.NameValue;
            outBody = dialog.BodyValue;
            return true;
        }
        outName = string.Empty;
        outBody = string.Empty;
        return false;
    }
}
