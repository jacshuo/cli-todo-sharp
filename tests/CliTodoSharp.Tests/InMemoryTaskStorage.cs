using CliTodoSharp.Models;
using CliTodoSharp.Services;

namespace CliTodoSharp.Tests;

/// <summary>
/// In-memory implementation of <see cref="ITaskStorageService"/> used by tests.
/// Operations are synchronous and isolated per test instance – no files touched.
/// </summary>
internal sealed class InMemoryTaskStorage : ITaskStorageService
{
    private List<TodoTask> _tasks;

    public string StoragePath => ":memory:";

    public InMemoryTaskStorage(IEnumerable<TodoTask>? seed = null)
    {
        _tasks = seed?.ToList() ?? [];
        AssignDisplayIndices();
    }

    public Task<List<TodoTask>> LoadAsync()
    {
        // Return deep copies so tests cannot accidentally mutate internal state
        // through object references that are still held by the manager.
        var copy = _tasks
            .Select(t => new TodoTask
            {
                Id           = t.Id,
                Title        = t.Title,
                Description  = t.Description,
                Tags         = [.. t.Tags],
                Status       = t.Status,
                Priority     = t.Priority,
                CreatedAt    = t.CreatedAt,
                DueDate      = t.DueDate,
                StartedAt    = t.StartedAt,
                CompletedAt  = t.CompletedAt,
                UpdatedAt    = t.UpdatedAt,
            })
            .OrderBy(t => t.CreatedAt)
            .ToList();

        int i = 1;
        foreach (var t in copy) t.DisplayIndex = i++;

        return Task.FromResult(copy);
    }

    public Task SaveAsync(List<TodoTask> tasks)
    {
        _tasks = [.. tasks];
        AssignDisplayIndices();
        return Task.CompletedTask;
    }

    private void AssignDisplayIndices()
    {
        int i = 1;
        foreach (var t in _tasks.OrderBy(t => t.CreatedAt))
            t.DisplayIndex = i++;
    }
}
