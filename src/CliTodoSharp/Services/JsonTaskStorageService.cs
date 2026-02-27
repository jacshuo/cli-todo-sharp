using System.Text.Json;
using System.Text.Json.Serialization;
using CliTodoSharp.Models;

namespace CliTodoSharp.Services;

/// <summary>
/// Reads and writes tasks as a pretty-printed JSON array so the file is
/// human-readable, diff-friendly, and trivially portable across platforms.
///
/// Storage priority (first wins):
///   1. Path passed to the constructor (set by --storage CLI flag)
///   2. TODO_STORAGE_PATH environment variable
///   3. Platform home dir: ~/.todo-sharp/tasks.json
///      (~/ on Linux/macOS, %USERPROFILE%\ on Windows)
/// </summary>
public sealed class JsonTaskStorageService : ITaskStorageService
{
    // ── JSON serialiser options ───────────────────────────────────────────────

    /// <summary>
    /// Shared, reusable <see cref="JsonSerializerOptions"/>.
    /// • WriteIndented   – makes the file human-readable and git-diffable
    /// • PropertyNameCaseInsensitive – tolerates minor manual edits to keys
    /// • Converters include a JsonStringEnumConverter so enum values are stored
    ///   as "Pending" / "Done" rather than 0 / 2, improving human readability.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly object _fileLock = new();   // guards concurrent file access

    /// <inheritdoc/>
    public string StoragePath { get; }

    // ── Construction ──────────────────────────────────────────────────────────

    public JsonTaskStorageService(string? explicitPath = null)
    {
        StoragePath = ResolveStoragePath(explicitPath);
    }

    // ── ITaskStorageService ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<TodoTask>> LoadAsync()
    {
        if (!File.Exists(StoragePath))
            return [];   // first run – return empty list

        string json;
        lock (_fileLock)
            json = File.ReadAllText(StoragePath);

        try
        {
            var tasks = JsonSerializer.Deserialize<List<TodoTask>>(json, JsonOptions)
                        ?? [];

            // Assign sequential display indices (sorted by creation time)
            // so users can reference tasks by short numbers in commands.
            AssignDisplayIndices(tasks);
            return tasks;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Could not parse task file '{StoragePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(List<TodoTask> tasks)
    {
        // Ensure the target directory exists (important on first run).
        var dir = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(tasks, JsonOptions);

        // Write to a temp file first, then atomically rename so a crash or
        // power failure during the write can never corrupt the data file.
        var tmp = StoragePath + ".tmp";
        lock (_fileLock)
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, StoragePath, overwrite: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Determine the storage path from the three-level priority chain.
    /// </summary>
    private static string ResolveStoragePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var envPath = Environment.GetEnvironmentVariable("TODO_STORAGE_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
            return Path.GetFullPath(envPath);

        // Platform-aware home directory:
        // RuntimeInformation is unnecessary here because the .NET BCL already
        // maps %USERPROFILE% on Windows and $HOME on POSIX via GetFolderPath.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".todo-sharp", "tasks.json");
    }

    /// <summary>
    /// Re-assigns 1-based sequential display indices ordered by CreatedAt.
    /// Call after every load so that the numbers shown to the user stay stable
    /// as long as the file has not changed between commands.
    /// </summary>
    private static void AssignDisplayIndices(List<TodoTask> tasks)
    {
        int i = 1;
        foreach (var t in tasks.OrderBy(t => t.CreatedAt))
            t.DisplayIndex = i++;
    }
}
