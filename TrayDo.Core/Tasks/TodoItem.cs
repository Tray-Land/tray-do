namespace TrayDo.Tasks;

/// <summary>One task. The same instance appears in "All tasks" and, when picked, on the hit list.</summary>
public sealed class TodoItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Text { get; set; } = string.Empty;

    public bool IsDone { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>Everything the app saves: every task, plus the hit list as an ordered list of task ids.</summary>
public sealed class TodoData
{
    /// <summary>Open tasks in display order (newest first); done tasks keep their slot but show below.</summary>
    public List<TodoItem> Items { get; set; } = [];

    public List<Guid> HitList { get; set; } = [];
}
