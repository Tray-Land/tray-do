using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrayDo.Tasks;
using Windows.UI.Text;

namespace TrayDo.Views;

/// <summary>
/// A row in either list. One instance per task is shared by both lists, so checking a task off on
/// the hit list shows immediately in "All tasks" too. The board stays the source of truth;
/// <see cref="Refresh"/> copies its state here after each change.
/// </summary>
public sealed partial class TodoItemViewModel(TodoItem item) : INotifyPropertyChanged
{
    private bool _isOnHitList;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TodoItem Item { get; } = item;

    public Guid Id => Item.Id;

    public string Text => Item.Text;

    /// <summary>
    /// Settable only so the check box can bind two-way and keep its binding after a click; the
    /// page applies the click to the board, and <see cref="Refresh"/> reports the result.
    /// </summary>
    public bool IsDone
    {
        get => Item.IsDone;
        set { }
    }

    public TextDecorations Decorations => IsDone ? TextDecorations.Strikethrough : TextDecorations.None;

    public double TextOpacity => IsDone ? 0.6 : 1.0;

    public bool IsOnHitList => _isOnHitList;

    public string HitListGlyph => _isOnHitList ? "" : ""; // FavoriteStarFill / FavoriteStar

    public string HitListLabel => _isOnHitList ? "Remove from hit list" : "Add to hit list";

    public string DoneLabel => IsDone ? $"Mark {Text} as not done" : $"Mark {Text} as done";

    /// <summary>ListViewItem uses this as the row's accessible name.</summary>
    public override string ToString() => Text;

    public void Refresh(bool isOnHitList)
    {
        _isOnHitList = isOnHitList;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(Decorations));
        OnPropertyChanged(nameof(TextOpacity));
        OnPropertyChanged(nameof(IsOnHitList));
        OnPropertyChanged(nameof(HitListGlyph));
        OnPropertyChanged(nameof(HitListLabel));
        OnPropertyChanged(nameof(DoneLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
