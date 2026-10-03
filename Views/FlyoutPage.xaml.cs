using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using TrayDo.Services;
using TrayDo.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace TrayDo.Views;

/// <summary>
/// The flyout's content: every task, and the hit list (an ordered pick of tasks to do next). All
/// changes go through <see cref="TodoStore.Board"/> and are saved right away; the lists here are
/// then synced to it with minimal moves so rows animate instead of redrawing.
/// </summary>
public sealed partial class FlyoutPage : Page, IDisposable
{
    private readonly ObservableCollection<TodoItemViewModel> _allRows = [];
    private readonly ObservableCollection<TodoItemViewModel> _hitRows = [];
    private readonly Dictionary<Guid, TodoItemViewModel> _rows = [];
    private readonly Flyout _editFlyout;
    private readonly TextBox _editBox;
    private TodoItemViewModel? _editing;
    private int _seenVersion = -1;

    public FlyoutPage()
    {
        InitializeComponent();
        TitleText.Text = App.DisplayName;
        AllList.ItemsSource = _allRows;
        HitList.ItemsSource = _hitRows;

        _editBox = new TextBox { Width = 280, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(_editBox, "Task text");
        _editBox.KeyDown += EditBox_KeyDown;
        _editFlyout = new Flyout { Content = _editBox, Placement = FlyoutPlacementMode.Bottom };
        _editFlyout.Closed += (_, _) => CommitEdit();

        Tabs.SelectedItem = SettingsService.ShowHitList ? HitTab : AllTab;
        Sync();
    }

    /// <summary>The settings button was clicked; the window swaps in the settings page.</summary>
    public event EventHandler? SettingsRequested;

    private static TodoBoard Board => TodoStore.Board;

    private bool IsHitListShown => Tabs.SelectedItem == HitTab;

    /// <summary>The flyout opened: catch up on changes made from the tray menu, and get ready to type.</summary>
    public void OnShown()
    {
        if (_seenVersion != TodoStore.Version)
        {
            Sync();
        }

        FooterText.Text = string.Empty;
        ShowProblem();
        AddBox.Focus(FocusState.Programmatic);
    }

    public void OnHidden() => _editFlyout.Hide();

    public void Dispose() => _editFlyout.Hide();

    // Adding

    private void AddBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            AddTyped();
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        AddTyped();
        AddBox.Focus(FocusState.Programmatic);
    }

    private void AddTyped()
    {
        if (Board.Add(AddBox.Text, IsHitListShown, DateTimeOffset.Now) is not { } item)
        {
            return;
        }

        AddBox.Text = string.Empty;
        Commit();
        ScrollIntoView(item.Id);
    }

    /// <summary>
    /// A paste of several lines (a list copied from another app) becomes one task per line. A
    /// single line pastes into the box as usual, minus any line break.
    /// </summary>
    private async void AddBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        // The clipboard read is async, so take over the paste now and redo it by hand if needed.
        e.Handled = true;
        string text;
        try
        {
            DataPackageView content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                return;
            }

            text = await content.GetTextAsync();
        }
        catch (Exception)
        {
            // The clipboard can be locked by another app for a moment.
            return;
        }

        IReadOnlyList<string> lines = PastedTasks.Split(text);
        if (lines.Count <= 1)
        {
            AddBox.SelectedText = text.ReplaceLineEndings(" ").Trim();
            AddBox.SelectionStart += AddBox.SelectionLength;
            AddBox.SelectionLength = 0;
            return;
        }

        IReadOnlyList<TodoItem> added = Board.Add(lines, IsHitListShown, DateTimeOffset.Now);
        Commit();
        FooterText.Text = IsHitListShown
            ? $"Added {added.Count} tasks to the hit list"
            : $"Added {added.Count} tasks";
        if (added.Count > 0)
        {
            ScrollIntoView(added[0].Id);
        }
    }

    // Row actions

    private void DoneCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: TodoItemViewModel row } box)
        {
            bool isDone = box.IsChecked == true;

            // Let the click finish before the row moves to its new place in the list.
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Board.SetDone(row.Id, isDone, DateTimeOffset.Now))
                {
                    Commit();
                }
            });
        }
    }

    private void HitListToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TodoItemViewModel row })
        {
            ToggleHitList(row);
        }
    }

    private void ToggleHitList(TodoItemViewModel row)
    {
        if (Board.IsOnHitList(row.Id) ? Board.RemoveFromHitList(row.Id) : Board.AddToHitList(row.Id))
        {
            Commit();
        }
    }

    private void Delete(TodoItemViewModel row)
    {
        if (Board.Remove(row.Id))
        {
            Commit();
            FooterText.Text = $"Deleted “{Truncate(row.Text)}”";
        }
    }

    private void Move(TodoItemViewModel row, int index)
    {
        if (Board.MoveInHitList(row.Id, index))
        {
            Commit();
        }
    }

    private void ItemMenu_Opening(object sender, object e)
    {
        MenuFlyout menu = (MenuFlyout)sender;
        menu.Items.Clear();
        if (menu.Target is not FrameworkElement { DataContext: TodoItemViewModel row } target)
        {
            return;
        }

        bool onHitList = Board.IsOnHitList(row.Id);
        menu.Items.Add(MenuItem("Edit", "", () => BeginEdit(row, target)));
        menu.Items.Add(MenuItem(row.IsDone ? "Mark as not done" : "Mark as done", row.IsDone ? "" : "", () =>
        {
            if (Board.SetDone(row.Id, !row.IsDone, DateTimeOffset.Now))
            {
                Commit();
            }
        }));
        menu.Items.Add(MenuItem(row.HitListLabel, onHitList ? "" : "", () => ToggleHitList(row)));

        if (onHitList && IsHitListShown)
        {
            int index = _hitRows.IndexOf(row);
    private void MoveOpen(TodoItemViewModel row, int index)
    {
        if (Board.MoveOpenTask(row.Id, index))
        {
            Commit();
            if (AllList.ContainerFromItem(row) is ListViewItem container)
            {
                container.Focus(FocusState.Keyboard);
            }
        }
    }

            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(MenuItem("Move to top", "", () => Move(row, 0), enabled: index > 0));
            menu.Items.Add(MenuItem("Move up", "", () => Move(row, index - 1), enabled: index > 0));
            menu.Items.Add(MenuItem("Move down", "", () => Move(row, index + 1), enabled: index < _hitRows.Count - 1));
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem("Delete", "", () => Delete(row)));
    }

    private static MenuFlyoutItem MenuItem(string text, string glyph, Action action, bool enabled = true)
    {
        MenuFlyoutItem item = new() { Text = text, Icon = new FontIcon { Glyph = glyph }, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Delete, F2 to edit, and on the hit list Alt+Up / Alt+Down to reorder without a mouse.</summary>
    private void List_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is not ListViewItem { Content: TodoItemViewModel row } container)
        {
            return;
        }

        bool alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        switch (e.Key)
        {
        if (!IsHitListShown && !row.IsDone)
        {
            int index = _allRows.IndexOf(row);
            int lastOpen = _allRows.Count(r => !r.IsDone) - 1;
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(MenuItem("Move to top", "", () => MoveOpen(row, 0), enabled: index > 0));
            menu.Items.Add(MenuItem("Move up", "", () => MoveOpen(row, index - 1), enabled: index > 0));
            menu.Items.Add(MenuItem("Move down", "", () => MoveOpen(row, index + 1), enabled: index < lastOpen));
        }

            case VirtualKey.Delete:
                Delete(row);
                break;
            case VirtualKey.F2:
                BeginEdit(row, container);
                break;
            case VirtualKey.Up when alt && ReferenceEquals(sender, HitList):
                Move(row, _hitRows.IndexOf(row) - 1);
                FocusRow(row);
                break;
            case VirtualKey.Down when alt && ReferenceEquals(sender, HitList):
                Move(row, _hitRows.IndexOf(row) + 1);
                FocusRow(row);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void HitList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        Board.SetHitListOrder(_hitRows.Select(r => r.Id));
        Commit();
    }

    private void ClearHitList_Click(object sender, RoutedEventArgs e)
    {
        int count = Board.ClearHitList();
        if (count > 0)
        {
            Commit();
            FooterText.Text = count == 1 ? "Took 1 task off the hit list" : $"Took {count} tasks off the hit list";
        }
    }

            case VirtualKey.Up when alt && ReferenceEquals(sender, AllList) && !row.IsDone:
                MoveOpen(row, _allRows.IndexOf(row) - 1);
                break;
            case VirtualKey.Down when alt && ReferenceEquals(sender, AllList) && !row.IsDone:
                MoveOpen(row, _allRows.IndexOf(row) + 1);
                break;
    private void DeleteCompleted_Click(object sender, RoutedEventArgs e)
    {
        int count = Board.RemoveCompleted();
        if (count > 0)
        {
            Commit();
            FooterText.Text = count == 1 ? "Deleted 1 completed task" : $"Deleted {count} completed tasks";
    private void AllList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (Board.SetOpenOrder(_allRows.Select(r => r.Id)))
        {
            Commit();
        }
        else
        {
            // Dropped somewhere that changes nothing (such as among the done tasks): snap back.
            Sync();
        }
    }

        }
    }

    // Editing

    private void BeginEdit(TodoItemViewModel row, FrameworkElement target)
    {
        _editing = row;
        _editBox.Text = row.Text;

        // From the context menu, wait for the menu to finish closing first.
        DispatcherQueue.TryEnqueue(() =>
        {
            _editFlyout.ShowAt(target);
            _editBox.SelectAll();
            _editBox.Focus(FocusState.Programmatic);
        });
    }

    private void EditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _editFlyout.Hide(); // Closed saves the edit.
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            _editing = null;
            _editFlyout.Hide();
        }
    }

    private void CommitEdit()
    {
        if (_editing is { } row && Board.Rename(row.Id, _editBox.Text))
        {
            Commit();
        }

        _editing = null;
    }

    // Tabs, saving and syncing

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        bool hit = IsHitListShown;
        AllView.Visibility = hit ? Visibility.Collapsed : Visibility.Visible;
        HitView.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
        AddBox.PlaceholderText = hit ? "Add to the hit list" : "Add a task";
        SettingsService.ShowHitList = hit;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void Commit()
    {
        TodoStore.Commit();
        Sync();
        ShowProblem();
    }

    /// <summary>Brings both lists, the counts and the empty states in line with the board.</summary>
    private void Sync()
    {
        TodoBoard board = Board;
        IReadOnlyList<TodoItem> all = board.AllItems;
        HashSet<Guid> live = [.. all.Select(i => i.Id)];
        foreach (Guid gone in _rows.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _rows.Remove(gone);
        }

        Apply(_allRows, [.. all.Select(RowFor)]);
        Apply(_hitRows, [.. board.HitListItems.Select(RowFor)]);

        HashSet<Guid> onHitList = [.. board.HitListItems.Select(i => i.Id)];
        foreach (TodoItemViewModel row in _rows.Values)
        {
            row.Refresh(onHitList.Contains(row.Id));
        }

        int remaining = board.HitListRemaining;
        int total = board.HitListCount;
        HitTab.Text = remaining > 0 ? $"Hit list ({remaining})" : "Hit list";
        HitSummaryText.Text = total == 0 ? string.Empty
            : remaining == 0 ? (total == 1 ? "Done. Nice work." : $"All {total} done. Nice work.")
            : $"{remaining} of {total} left · drag to reorder";
        HitHeader.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
        HitEmptyText.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        AllEmptyText.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DeleteCompletedItem.IsEnabled = board.CompletedCount > 0;
        _seenVersion = TodoStore.Version;
    }

    private TodoItemViewModel RowFor(TodoItem item)
    {
        if (!_rows.TryGetValue(item.Id, out TodoItemViewModel? row))
        {
            row = new TodoItemViewModel(item);
            _rows.Add(item.Id, row);
        }

        return row;
    }

    /// <summary>Reshapes <paramref name="rows"/> into <paramref name="desired"/> with removes, inserts and moves only.</summary>
    private static void Apply(ObservableCollection<TodoItemViewModel> rows, List<TodoItemViewModel> desired)
    {
        HashSet<TodoItemViewModel> keep = [.. desired];
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(rows[i]))
            {
                rows.RemoveAt(i);
            }
        }

        for (int i = 0; i < desired.Count; i++)
        {
            if (i < rows.Count && rows[i] == desired[i])
            {
                continue;
            }

            int at = rows.IndexOf(desired[i]);
            if (at < 0)
            {
                rows.Insert(i, desired[i]);
            }
            else
            {
                rows.Move(at, i);
            }
        }
    }

    private void ShowProblem()
    {
        StatusBar.Title = TodoStore.Problem is null ? string.Empty : "Something went wrong";
        StatusBar.Message = TodoStore.Problem ?? string.Empty;
        StatusBar.IsOpen = TodoStore.Problem is not null;
    }

    private void ScrollIntoView(Guid id)
    {
        if (_rows.TryGetValue(id, out TodoItemViewModel? row))
        {
            (IsHitListShown ? HitList : AllList).ScrollIntoView(row);
        }
    }

    private void FocusRow(TodoItemViewModel row)
    {
        if (HitList.ContainerFromItem(row) is ListViewItem container)
        {
            container.Focus(FocusState.Keyboard);
        }
    }

    private static string Truncate(string text) => text.Length <= 40 ? text : text[..39] + "…";
}
