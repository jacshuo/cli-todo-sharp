using CliTodoSharp.Models;

namespace CliTodoSharp.Services;

/// <summary>
/// Abstraction over the persistence layer.
/// Decoupling the interface from the JSON implementation lets us swap or
/// mock the backend in tests without touching any command code.
/// </summary>
public interface ITaskStorageService
{
    /// <summary>Absolute path of the data file currently in use.</summary>
    string StoragePath { get; }

    /// <summary>Load all tasks from the backing store.</summary>
    Task<List<TodoTask>> LoadAsync();

    /// <summary>Persist the full task list, replacing any existing data.</summary>
    Task SaveAsync(List<TodoTask> tasks);
}
