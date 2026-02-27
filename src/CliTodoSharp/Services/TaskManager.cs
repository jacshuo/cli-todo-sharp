using CliTodoSharp.Models;

namespace CliTodoSharp.Services;

/// <summary>
/// High-level business-logic layer that sits between the CLI commands and the
/// raw storage service.  Every mutating operation loads → mutates → saves,
/// keeping the JSON file always up-to-date after each command.
///
/// Commands resolve a task by its short *display index* (the 1-based numbers
/// shown in <c>todo list</c>) rather than the full GUID, making the CLI
/// ergonomic while the GUIDs remain stable in the JSON file.
/// </summary>
public sealed class TaskManager(ITaskStorageService storage)
{
    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>Return all tasks, sorted by creation date (oldest first).</summary>
    public async Task<List<TodoTask>> GetAllAsync()
        => (await storage.LoadAsync())
           .OrderBy(t => t.CreatedAt)
           .ToList();

    /// <summary>
    /// Return a filtered + optionally sorted view.
    /// </summary>
    /// <param name="filter">Null = all statuses; otherwise restrict to that status.</param>
    /// <param name="includeOverdue">
    ///     When true the filter also includes Pending/InProgress tasks whose
    ///     DueDate has passed (i.e. those the UI renders as "Overdue").
    /// </param>
    /// <param name="tag">Optional tag filter (case-insensitive).</param>
    /// <param name="sortBy">
    ///     One of: "created" (default), "due", "priority", "title", "status".
    /// </param>
    public async Task<List<TodoTask>> GetFilteredAsync(
        TodoStatus? filter = null,
        bool includeOverdue = false,
        string? tag = null,
        string sortBy = "created")
    {
        var tasks = await storage.LoadAsync();

        // ── Status filter ─────────────────────────────────────────────────────
        if (filter.HasValue)
        {
            tasks = tasks
                .Where(t => t.Status == filter.Value
                            || (includeOverdue && t.IsOverdue))
                .ToList();
        }

        // ── Tag filter ────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(tag))
        {
            tasks = tasks
                .Where(t => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        // ── Sorting ───────────────────────────────────────────────────────────
        tasks = sortBy.ToLowerInvariant() switch
        {
            "due"      => tasks.OrderBy(t => t.DueDate ?? DateTime.MaxValue).ToList(),
            "priority" => tasks.OrderByDescending(t => (int)t.Priority).ToList(),
            "title"    => tasks.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            "status"   => tasks.OrderBy(t => (int)t.Status).ToList(),
            _          => tasks.OrderBy(t => t.CreatedAt).ToList(),
        };

        return tasks;
    }

    /// <summary>Resolve a task by display index.  Throws on not found.</summary>
    public async Task<TodoTask> GetByIndexAsync(int displayIndex)
    {
        var tasks = await storage.LoadAsync();
        return tasks.FirstOrDefault(t => t.DisplayIndex == displayIndex)
               ?? throw new KeyNotFoundException(
                   $"No task with index #{displayIndex}. Run 'todo list' to see valid indices.");
    }

    /// <summary>Resolve a task by full or partial GUID prefix.</summary>
    public async Task<TodoTask> GetByIdAsync(string idPrefix)
    {
        var tasks = await storage.LoadAsync();
        var prefix = idPrefix.ToLowerInvariant();
        var matches = tasks
            .Where(t => t.Id.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => throw new KeyNotFoundException($"No task matching id '{idPrefix}'."),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Multiple tasks match the prefix '{idPrefix}'. Provide more characters."),
        };
    }

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Add a new task and persist.
    /// Returns the newly created task (with DisplayIndex assigned).
    /// </summary>
    public async Task<TodoTask> AddAsync(
        string title,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null,
        IEnumerable<string>? tags = null)
    {
        var tasks = await storage.LoadAsync();

        var task = new TodoTask
        {
            Title       = title.Trim(),
            Description = description?.Trim(),
            Priority    = priority,
            DueDate     = dueDate,
            Tags        = tags?.Select(t => t.Trim().ToLowerInvariant()).ToList() ?? [],
        };

        tasks.Add(task);
        await storage.SaveAsync(tasks);

        // Reload to get correctly-assigned DisplayIndex back to the caller
        return (await GetAllAsync()).First(t => t.Id == task.Id);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    /// <summary>Apply arbitrary property edits; persists immediately.</summary>
    public async Task<TodoTask> EditAsync(
        int displayIndex,
        string? newTitle = null,
        string? newDescription = null,
        TaskPriority? newPriority = null,
        DateTime? newDueDate = null,
        bool clearDueDate = false,
        IEnumerable<string>? newTags = null)
    {
        var tasks = await storage.LoadAsync();
        var task  = tasks.FirstOrDefault(t => t.DisplayIndex == displayIndex)
                    ?? throw new KeyNotFoundException(
                        $"No task with index #{displayIndex}.");

        if (newTitle is not null)       task.Title       = newTitle.Trim();
        if (newDescription is not null) task.Description = newDescription.Trim();
        if (newPriority.HasValue)       task.Priority    = newPriority.Value;
        if (clearDueDate)               task.DueDate     = null;
        else if (newDueDate.HasValue)   task.DueDate     = newDueDate.Value;
        if (newTags is not null)
            task.Tags = newTags.Select(t => t.Trim().ToLowerInvariant()).ToList();

        task.UpdatedAt = DateTime.UtcNow;
        await storage.SaveAsync(tasks);

        return (await GetAllAsync()).First(t => t.Id == task.Id);
    }

    // ── Status transitions ────────────────────────────────────────────────────

    /// <summary>Transition a task to InProgress.</summary>
    public async Task<TodoTask> StartAsync(int displayIndex)
    {
        var (tasks, task) = await LoadAndFind(displayIndex);
        Guard(task, TodoStatus.InProgress, allowedFrom: [TodoStatus.Pending]);

        task.Status    = TodoStatus.InProgress;
        task.StartedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        await storage.SaveAsync(tasks);
        return (await GetAllAsync()).First(t => t.Id == task.Id);
    }

    /// <summary>Transition a task to Done.</summary>
    public async Task<TodoTask> CompleteAsync(int displayIndex)
    {
        var (tasks, task) = await LoadAndFind(displayIndex);
        Guard(task, TodoStatus.Done,
              allowedFrom: [TodoStatus.Pending, TodoStatus.InProgress]);

        task.Status      = TodoStatus.Done;
        task.CompletedAt = DateTime.UtcNow;
        task.UpdatedAt   = DateTime.UtcNow;
        await storage.SaveAsync(tasks);
        return (await GetAllAsync()).First(t => t.Id == task.Id);
    }

    /// <summary>Transition a task to Canceled.</summary>
    public async Task<TodoTask> CancelAsync(int displayIndex)
    {
        var (tasks, task) = await LoadAndFind(displayIndex);
        Guard(task, TodoStatus.Canceled,
              allowedFrom: [TodoStatus.Pending, TodoStatus.InProgress]);

        task.Status    = TodoStatus.Canceled;
        task.UpdatedAt = DateTime.UtcNow;
        await storage.SaveAsync(tasks);
        return (await GetAllAsync()).First(t => t.Id == task.Id);
    }

    /// <summary>Reset a Done or Canceled task back to Pending.</summary>
    public async Task<TodoTask> ReopenAsync(int displayIndex)
    {
        var (tasks, task) = await LoadAndFind(displayIndex);
        Guard(task, TodoStatus.Pending,
              allowedFrom: [TodoStatus.Done, TodoStatus.Canceled]);

        task.Status      = TodoStatus.Pending;
        task.CompletedAt = null;
        task.StartedAt   = null;
        task.UpdatedAt   = DateTime.UtcNow;
        await storage.SaveAsync(tasks);
        return (await GetAllAsync()).First(t => t.Id == task.Id);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    /// <summary>Permanently remove a task.</summary>
    public async Task<TodoTask> RemoveAsync(int displayIndex)
    {
        var tasks = await storage.LoadAsync();
        var task  = tasks.FirstOrDefault(t => t.DisplayIndex == displayIndex)
                    ?? throw new KeyNotFoundException($"No task with index #{displayIndex}.");
        tasks.Remove(task);
        await storage.SaveAsync(tasks);
        return task;
    }

    /// <summary>Delete all Done and Canceled tasks at once.</summary>
    public async Task<int> PurgeAsync()
    {
        var tasks    = await storage.LoadAsync();
        var before   = tasks.Count;
        tasks.RemoveAll(t => t.Status is TodoStatus.Done or TodoStatus.Canceled);
        await storage.SaveAsync(tasks);
        return before - tasks.Count;
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Return a snapshot of task counts grouped by status.
    /// The overdue count is separate because the underlying status is still
    /// Pending/InProgress – we just surface it for the progress display.
    /// </summary>
    public async Task<TaskStats> GetStatsAsync()
    {
        var tasks = await storage.LoadAsync();
        return new TaskStats
        {
            Total     = tasks.Count,
            Pending   = tasks.Count(t => t.Status == TodoStatus.Pending && !t.IsOverdue),
            InProgress= tasks.Count(t => t.Status == TodoStatus.InProgress && !t.IsOverdue),
            Done      = tasks.Count(t => t.Status == TodoStatus.Done),
            Canceled  = tasks.Count(t => t.Status == TodoStatus.Canceled),
            Overdue   = tasks.Count(t => t.IsOverdue),
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<(List<TodoTask> tasks, TodoTask task)> LoadAndFind(int displayIndex)
    {
        var tasks = await storage.LoadAsync();
        var task  = tasks.FirstOrDefault(t => t.DisplayIndex == displayIndex)
                    ?? throw new KeyNotFoundException($"No task with index #{displayIndex}.");
        return (tasks, task);
    }

    /// <summary>
    /// Validate state-machine transitions to prevent nonsensical moves.
    /// For example, you cannot mark a Canceled task as Done without reopening it first.
    /// </summary>
    private static void Guard(TodoTask task, TodoStatus target, TodoStatus[] allowedFrom)
    {
        if (task.Status == target)
            throw new InvalidOperationException(
                $"Task #{task.DisplayIndex} is already '{target}'.");

        if (!allowedFrom.Contains(task.Status))
            throw new InvalidOperationException(
                $"Cannot move task from '{task.Status}' to '{target}'. " +
                $"Allowed source states: {string.Join(", ", allowedFrom)}.");
    }
}

// ── Supporting value type ─────────────────────────────────────────────────────

/// <summary>Snapshot of task counts used by the stats command and renderer.</summary>
public sealed record TaskStats
{
    public int Total      { get; init; }
    public int Pending    { get; init; }
    public int InProgress { get; init; }
    public int Done       { get; init; }
    public int Canceled   { get; init; }
    public int Overdue    { get; init; }

    /// <summary>
    /// Completion rate as a value between 0 and 1.
    /// Tasks that are Canceled are excluded from the denominator because they
    /// were never meant to be finished.
    /// </summary>
    public double CompletionRate =>
        (Total - Canceled) == 0
            ? 0
            : (double)Done / (Total - Canceled);
}
