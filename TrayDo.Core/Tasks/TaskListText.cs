namespace TrayDo.Tasks;

/// <summary>Formats tasks as a Markdown task list, the reverse of <see cref="PastedTasks"/>.</summary>
public static class TaskListText
{
    /// <summary>One "- [ ] text" line per task, or an empty string for none.</summary>
    public static string ToMarkdown(IEnumerable<TodoItem> items) =>
        string.Join('\n', items.Select(i => $"- [ ] {i.Text.ReplaceLineEndings(" ")}"));
}
