namespace CliTodoSharp.Models;

/// <summary>
/// Urgency level of a task. Used when sorting and colour-coding task lists.
/// Values are ordered from lowest (None) to highest (Critical) so that natural
/// numeric comparisons (>, <) reflect priority ordering.
/// </summary>
public enum TaskPriority
{
    None     = 0,
    Low      = 1,
    Medium   = 2,
    High     = 3,
    Critical = 4,
}
