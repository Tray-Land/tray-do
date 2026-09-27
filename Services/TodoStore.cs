using TrayDo.Tasks;
using Windows.Storage;

namespace TrayDo.Services;

/// <summary>
/// The app's one copy of the task list, loaded on first use and saved after every change to
/// <c>LocalState\tasks.json</c>. It stays in memory for the app's life: a to-do list is a few
/// kilobytes, and the tray badge needs the hit list count while no window is open.
/// </summary>
internal static class TodoStore
{
    private static TodoBoard? _board;

    /// <summary>Raised on the UI thread after each saved change; the tray badge follows it.</summary>
    public static event EventHandler? Changed;

    public static TodoBoard Board => _board ??= new TodoBoard(Load());

    /// <summary>Bumps on every change, so a view can tell whether something else edited the list while it was hidden.</summary>
    public static int Version { get; private set; }

    /// <summary>Why the last load or save failed, or null when the file is fine.</summary>
    public static string? Problem { get; private set; }

    private static string FilePath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "tasks.json");

    /// <summary>Saves the board after a change and tells listeners. Returns false if the save failed.</summary>
    public static bool Commit()
    {
        Version++;
        try
        {
            TodoFile.Save(FilePath, Board.Data);
            Problem = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Problem = $"Couldn't save your tasks. {ex.Message}";
        }

        Changed?.Invoke(null, EventArgs.Empty);
        return Problem is null;
    }

    private static TodoData Load()
    {
        try
        {
            return TodoFile.Load(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Problem = $"Couldn't open your tasks. {ex.Message}";
            return new TodoData();
        }
    }
}
