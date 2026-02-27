namespace CliTodoSharp.Models;

/// <summary>
/// Represents the lifecycle state of a to-do task.
/// "Overdue" is NOT stored here; it is a *derived display state* computed at
/// render time by checking whether a Pending or InProgress task has a DueDate
/// that has already passed. This keeps the persisted JSON clean and avoids
/// stale status values when the file is opened days later.
/// </summary>
public enum TodoStatus
{
    /// <summary>Task has been created but work has not started.</summary>
    Pending,

    /// <summary>Work is actively underway on this task.</summary>
    InProgress,

    /// <summary>Task has been completed successfully.</summary>
    Done,

    /// <summary>Task was deliberately abandoned or is no longer relevant.</summary>
    Canceled,
}
