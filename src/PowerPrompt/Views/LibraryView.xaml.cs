using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PowerPrompt.Common;
using PowerPrompt.Library;
using PowerPrompt.Models;
using PowerPrompt.Views.Dialogs;

namespace PowerPrompt.Views;

/// <summary>
/// Feature B view: categories → prompts → editable detail. Keyboard: ↑/↓ move,
/// →/Enter drill from categories into prompts, ← back, Enter on a prompt copies its
/// body. The right pane shows the selected prompt's body and lets you edit it in
/// place (Save, or auto-saved when you switch away).
/// </summary>
public partial class LibraryView : UserControl
{
    private readonly LibraryStore _store;
    private readonly DispatcherTimer _noteTimer;

    private bool _loadingEditor;
    private bool _refreshing;
    private bool _dirty;
    private Prompt? _editing;

    public LibraryView(LibraryStore store)
    {
        InitializeComponent();
        _store = store;
        _noteTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
        _noteTimer.Tick += (_, _) => { _noteTimer.Stop(); EditorNote.Visibility = Visibility.Collapsed; };

        // In-app shortcut: Ctrl+F jumps to the search box (not a global hotkey).
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
        };

        Loaded += (_, _) => { RefreshCategories(); FocusCategories(); };
    }

    private bool IsSearching => SearchBox.Text.Trim().Length > 0;
    private string? SelectedCategory => CategoriesList.SelectedItem as string;

    public void Reload()
    {
        SaveDirty();
        _store.Load();
        RefreshCategories();
        FocusCategories();
    }

    public void FocusCategories()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (CategoriesList.SelectedItem is { } sel &&
                CategoriesList.ItemContainerGenerator.ContainerFromItem(sel) is ListBoxItem item)
                item.Focus();
            else
                CategoriesList.Focus();
        }), DispatcherPriority.Input);
    }

    private void RefreshCategories()
    {
        string? previous = SelectedCategory;
        var categories = _store.Categories;
        _refreshing = true;
        CategoriesList.ItemsSource = categories;
        NoCategories.Visibility = categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (categories.Count > 0)
            CategoriesList.SelectedItem = previous is not null && categories.Contains(previous) ? previous : categories[0];
        _refreshing = false;
        RefreshPrompts();
    }

    private void RefreshPrompts(string? selectSlug = null)
    {
        IReadOnlyList<Prompt> items;
        if (IsSearching)
        {
            items = _store.Search(SearchBox.Text);
            PromptsHeader.Text = $"RESULTS ({items.Count})";
            SetEmptyPrompts(items.Count == 0, "No matches");
        }
        else if (SelectedCategory is { } category)
        {
            items = _store.PromptsIn(category);
            PromptsHeader.Text = "PROMPTS";
            SetEmptyPrompts(items.Count == 0, "No prompts yet — add one");
        }
        else
        {
            items = Array.Empty<Prompt>();
            PromptsHeader.Text = "PROMPTS";
            SetEmptyPrompts(_store.Categories.Count > 0, "Select a category");
        }

        _refreshing = true;
        PromptsList.ItemsSource = items;
        Prompt? sel = null;
        if (selectSlug is not null)
            sel = items.FirstOrDefault(p => p.Slug == selectSlug);
        sel ??= items.Count > 0 ? items[0] : null;
        PromptsList.SelectedItem = sel;
        _refreshing = false;

        LoadEditor(sel);
    }

    private void SetEmptyPrompts(bool show, string message)
    {
        EmptyPrompts.Text = message;
        EmptyPrompts.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- Editor ----

    private void LoadEditor(Prompt? prompt)
    {
        _loadingEditor = true;
        _editing = prompt;
        _dirty = false;
        SaveButton.IsEnabled = false;
        EditorNote.Visibility = Visibility.Collapsed;

        if (prompt is null)
        {
            EditorPanel.Visibility = Visibility.Collapsed;
            EditorPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            EditorPlaceholder.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Visible;
            NameBox.Text = prompt.Name;
            BodyBox.Text = prompt.Body;
        }
        _loadingEditor = false;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingEditor)
            return;
        _dirty = true;
        SaveButton.IsEnabled = true;
        EditorNote.Visibility = Visibility.Collapsed;
    }

    /// <summary>Persists pending editor edits. Returns true if something was saved.</summary>
    private bool SaveDirty()
    {
        if (_editing is null || !_dirty || string.IsNullOrWhiteSpace(NameBox.Text))
            return false;
        _store.UpdatePrompt(_editing, NameBox.Text, BodyBox.Text);
        _dirty = false;
        SaveButton.IsEnabled = false;
        return true;
    }

    private void SavePrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is null)
            return;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("A prompt needs a name.", "PowerPrompt");
            return;
        }
        string slug = _editing.Slug;
        _store.UpdatePrompt(_editing, NameBox.Text, BodyBox.Text);
        _dirty = false;
        RefreshPrompts(slug);
        ShowNote("Saved ✓");
    }

    private void ShowNote(string text)
    {
        EditorNote.Text = text;
        EditorNote.Visibility = Visibility.Visible;
        _noteTimer.Stop();
        _noteTimer.Start();
    }

    // ---- Navigation ----

    private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing)
            return;
        SaveDirty();
        if (!IsSearching)
            RefreshPrompts();
    }

    private void PromptsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing)
            return;
        var newSel = PromptsList.SelectedItem as Prompt;
        if (SaveDirty())
            RefreshPrompts(newSel?.Slug);
        else
            LoadEditor(newSel);
    }

    private void CategoriesList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Right or Key.Enter && PromptsList.Items.Count > 0)
        {
            FocusPrompt(0);
            e.Handled = true;
        }
    }

    private void PromptsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Left)
        {
            CategoriesList.Focus();
            if (CategoriesList.SelectedItem is { } sel &&
                CategoriesList.ItemContainerGenerator.ContainerFromItem(sel) is ListBoxItem item)
                item.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CopySelectedPrompt();
            e.Handled = true;
        }
    }

    private void FocusPrompt(int index)
    {
        PromptsList.SelectedIndex = index;
        if (PromptsList.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
            item.Focus();
        else
            PromptsList.Focus();
    }

    // ---- Copy ----

    private void CopyPrompt_Click(object sender, RoutedEventArgs e) => CopySelectedPrompt();

    private void CopySelectedPrompt()
    {
        if (_editing is null)
            return;
        ClipboardHelper.SetText(BodyBox.Text);
        ShowNote("Copied ✓");
    }

    // ---- Category CRUD ----

    private void NewCategory_Click(object sender, RoutedEventArgs e)
    {
        if (InputDialog.TryGetName(Window.GetWindow(this)!, "New category", "Category name", string.Empty, out var name))
        {
            if (!_store.CreateCategory(name))
                MessageBox.Show("Could not create category (empty or already exists).", "PowerPrompt");
            RefreshCategories();
            CategoriesList.SelectedItem = name;
        }
    }

    private void RenameCategory_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCategory is not { } category)
            return;
        if (InputDialog.TryGetName(Window.GetWindow(this)!, "Rename category", "New name", category, out var name))
        {
            if (!_store.RenameCategory(category, name))
                MessageBox.Show("Could not rename (empty or name already exists).", "PowerPrompt");
            RefreshCategories();
            CategoriesList.SelectedItem = name;
        }
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCategory is not { } category)
            return;

        int count = _store.PromptsIn(category).Count;
        if (count == 0)
        {
            if (MessageBox.Show($"Delete empty category \"{category}\"?", "PowerPrompt",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _store.DeleteCategory(category, CategoryDeleteMode.DeletePrompts);
                RefreshCategories();
            }
            return;
        }

        var others = _store.Categories.Where(c => c != category).ToList();
        if (DeleteCategoryDialog.Ask(Window.GetWindow(this)!, category, count, others, out var mode, out var target))
        {
            _store.DeleteCategory(category, mode, target);
            RefreshCategories();
        }
    }

    // ---- Prompt CRUD ----

    private void NewPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCategory is not { } category)
        {
            MessageBox.Show("Create a category first.", "PowerPrompt");
            return;
        }
        SaveDirty();
        var created = _store.CreatePrompt(category, "New prompt", string.Empty);
        if (created is null)
            return;
        RefreshPrompts(created.Slug);
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void MovePrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is not { } prompt)
            return;
        var targets = _store.Categories.Where(c => c != prompt.Category).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("No other category to move to.", "PowerPrompt");
            return;
        }
        if (ChooseCategoryDialog.TryChoose(Window.GetWindow(this)!, "Move prompt", "Move to category:", targets, out var target))
        {
            _store.MovePrompt(prompt, target);
            RefreshPrompts();
        }
    }

    private void DeletePrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is not { } prompt)
            return;
        if (MessageBox.Show($"Delete prompt \"{prompt.Name}\"?", "PowerPrompt",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _dirty = false;
            _store.DeletePrompt(prompt);
            RefreshPrompts();
        }
    }

    // ---- Search ----

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        SaveDirty();
        RefreshPrompts();
    }
}
