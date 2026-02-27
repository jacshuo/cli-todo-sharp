using System.Text.Json.Serialization;

namespace CliTodoSharp.Models;

/// <summary>
/// The core domain object – one to-do item persisted in the JSON store.
/// All DateTime values are stored as UTC ISO-8601 strings so the file
/// round-trips correctly when moved between machines in different time zones.
/// </summary>
public sealed class TodoTask
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Stable, globally-unique identifier. Generated once on creation.
    /// We use a full GUID so that tasks remain unambiguous when files from
    /// multiple machines are merged.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Short human-readable number (1, 2, 3 …) regenerated each load from the
    /// sorted creation order. NOT persisted – referenced only in the UI so that
    /// users can type "todo done 3" instead of a full GUID.
    /// </summary>
    [JsonIgnore]
    public int DisplayIndex { get; set; }

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>Required short title shown in list views.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional multi-line detail text.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Arbitrary string tags for grouping/filtering (e.g. "work", "home").
    /// </summary>
    public List<string> Tags { get; set; } = [];

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Current lifecycle state (see <see cref="TodoStatus"/>).</summary>
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    /// <summary>Urgency level used for sorting and colour coding.</summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>UTC instant when the task was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional UTC deadline. When this date is in the past and the task is
    /// still Pending or InProgress, the UI renders it as "Overdue".
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>UTC instant when the task moved to InProgress.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>UTC instant when the task moved to Done.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>UTC instant of the last property change.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Derived helpers (not serialised) ──────────────────────────────────────

    /// <summary>
    /// True when the task has missed its deadline and is not yet finished.
    /// Computed at runtime; never stored to avoid stale values.
    /// </summary>
    [JsonIgnore]
    public bool IsOverdue =>
        DueDate.HasValue
        && DueDate.Value < DateTime.UtcNow
        && Status is TodoStatus.Pending or TodoStatus.InProgress;

    /// <summary>
    /// Effective display status: returns "Overdue" label semantics while
    /// preserving the real stored <see cref="Status"/> value on the object.
    /// Use this only in the renderer – business logic should use Status directly.
    /// </summary>
    [JsonIgnore]
    public string EffectiveStatusLabel => IsOverdue ? "Overdue" : Status.ToString();
}
