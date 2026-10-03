namespace TrayDo.Tasks;

/// <summary>
/// The task list and the hit list, and every change the UI can make to them. The hit list is an
/// ordered subset of the tasks: removing a task from it (or clearing it) never deletes or completes
/// the task itself.
/// </summary>
public sealed class TodoBoard
{
    private readonly TodoData _data;
    private readonly Dictionary<Guid, TodoItem> _byId;

    public TodoBoard(TodoData? data = null)
    {
        _data = data ?? new TodoData();
        _data.Items ??= [];
        _data.HitList ??= [];

        // A hand-edited or partly written file may hold duplicates, blanks, or ids with no task.
        _data.Items.RemoveAll(i => i is null || string.IsNullOrWhiteSpace(i.Text));
        _byId = new();
        _data.Items.RemoveAll(i => !_byId.TryAdd(i.Id, i));
        HashSet<Guid> seen = [];
        _data.HitList.RemoveAll(id => !_byId.ContainsKey(id) || !seen.Add(id));
    }

    public TodoData Data => _data;

    /// <summary>Open tasks first (in list order), then done tasks, most recently completed first.</summary>
    public IReadOnlyList<TodoItem> AllItems =>
    [
        .. _data.Items.Where(i => !i.IsDone),
        .. _data.Items.Where(i => i.IsDone).OrderByDescending(i => i.CompletedAt ?? DateTimeOffset.MinValue),
    ];

    /// <summary>Hit list tasks in the user's order; done ones stay in place until the list is cleared.</summary>
    public IReadOnlyList<TodoItem> HitListItems => [.. _data.HitList.Select(id => _byId[id])];

    public int HitListCount => _data.HitList.Count;

    /// <summary>What the tray badge shows.</summary>
    public int HitListRemaining => _data.HitList.Count(id => !_byId[id].IsDone);

    public int CompletedCount => _data.Items.Count(i => i.IsDone);

    public TodoItem? Find(Guid id) => _byId.GetValueOrDefault(id);

    public bool IsOnHitList(Guid id) => _data.HitList.Contains(id);

    /// <summary>
    /// Adds one task per non-blank text, above the existing tasks and in the given order, so a
    /// pasted list reads the same way it did where it came from. With <paramref name="addToHitList"/>
    /// they also join the end of the hit list.
    /// </summary>
    public IReadOnlyList<TodoItem> Add(IEnumerable<string> texts, bool addToHitList, DateTimeOffset now)
    {
        List<TodoItem> added = [];
        foreach (string text in texts)
        {
            string trimmed = text?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                continue;
            }

            TodoItem item = new() { Text = trimmed, CreatedAt = now };
            added.Add(item);
            _byId.Add(item.Id, item);
        }

        _data.Items.InsertRange(0, added);
        if (addToHitList)
        {
            _data.HitList.AddRange(added.Select(i => i.Id));
        }

        return added;
    }

    public TodoItem? Add(string text, bool addToHitList, DateTimeOffset now) =>
        Add([text], addToHitList, now) is [var item] ? item : null;

    public bool SetDone(Guid id, bool isDone, DateTimeOffset now)
    {
        if (Find(id) is not { } item || item.IsDone == isDone)
        {
            return false;
        }

        item.IsDone = isDone;
        item.CompletedAt = isDone ? now : null;
        if (!isDone)
        {
            // Reopened tasks go back to the top of the open list rather than wherever they were.
            _data.Items.Remove(item);
            _data.Items.Insert(0, item);
        }

        return true;
    }

    public bool Rename(Guid id, string text)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || Find(id) is not { } item || item.Text == trimmed)
        {
            return false;
        }

        item.Text = trimmed;
        return true;
    }

    public bool Remove(Guid id)
    {
        if (!_byId.Remove(id, out TodoItem? item))
        {
            return false;
        }

        _data.Items.Remove(item);
        _data.HitList.Remove(id);
        return true;
    }

    /// <summary>Deletes every done task, including any still on the hit list.</summary>
    public int RemoveCompleted()
    {
        List<Guid> done = [.. _data.Items.Where(i => i.IsDone).Select(i => i.Id)];
        foreach (Guid id in done)
        {
            Remove(id);
        }

        return done.Count;
    }

    /// <summary>Appends the task to the end of the hit list.</summary>
    public bool AddToHitList(Guid id)
    {
        if (!_byId.ContainsKey(id) || _data.HitList.Contains(id))
        {
            return false;
        }

        _data.HitList.Add(id);
        return true;
    }

    public bool RemoveFromHitList(Guid id) => _data.HitList.Remove(id);

    /// <summary>Moves a hit list task to <paramref name="index"/> (clamped to the list).</summary>
    public bool MoveInHitList(Guid id, int index)
    {
        int from = _data.HitList.IndexOf(id);
        if (from < 0)
        {
            return false;
        }

        int to = Math.Clamp(index, 0, _data.HitList.Count - 1);
        if (from == to)
        {
            return false;
        }

        _data.HitList.RemoveAt(from);
        _data.HitList.Insert(to, id);
        return true;
    }

    /// <summary>
    /// Takes the hit list order from a drag-reorder. Ids the list doesn't hold are ignored, and any
    /// it holds that are missing keep their relative order at the end, so nothing drops off.
    /// </summary>
    public void SetHitListOrder(IEnumerable<Guid> order)
    {
        HashSet<Guid> current = [.. _data.HitList];
        List<Guid> reordered = [.. order.Where(current.Remove)];
        reordered.AddRange(_data.HitList.Where(current.Contains));
        _data.HitList = reordered;
    }

    /// <summary>
    /// Takes the order of the open tasks from a drag-reorder of "All tasks". Done tasks are ignored
    /// (they sort by completion time), and open tasks missing from <paramref name="order"/> keep
    /// their relative order after the listed ones, so nothing drops off.
    /// </summary>
    public bool SetOpenOrder(IEnumerable<Guid> order)
    {
        Dictionary<Guid, TodoItem> open = _data.Items.Where(i => !i.IsDone).ToDictionary(i => i.Id);
        List<TodoItem> reordered = [.. order.Where(open.ContainsKey).Distinct().Select(id => open[id])];
        HashSet<Guid> placed = [.. reordered.Select(i => i.Id)];
        reordered.AddRange(_data.Items.Where(i => !i.IsDone && !placed.Contains(i.Id)));

        if (reordered.SequenceEqual(_data.Items.Where(i => !i.IsDone)))
        {
            return false;
        }

        _data.Items = [.. reordered, .. _data.Items.Where(i => i.IsDone)];
        return true;
    }

    /// <summary>Moves an open task to <paramref name="index"/> among the open tasks (clamped).</summary>
    public bool MoveOpenTask(Guid id, int index)
    {
        List<Guid> open = [.. _data.Items.Where(i => !i.IsDone).Select(i => i.Id)];
        int from = open.IndexOf(id);
        if (from < 0)
        {
            return false;
        }

        open.RemoveAt(from);
        open.Insert(Math.Clamp(index, 0, open.Count), id);
        return SetOpenOrder(open);
    }

    /// <summary>Empties the hit list. The tasks themselves are untouched.</summary>
    public int ClearHitList()
    {
        int count = _data.HitList.Count;
        _data.HitList.Clear();
        return count;
    }
}
