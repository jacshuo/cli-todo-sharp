using System.ComponentModel;
using CliTodoSharp.Models;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo list [--status pending] [--tag work] [--sort due]</c>
/// (alias: <c>todo ls</c>)
///
/// Displays a colour-coded table of tasks with optional filters.
/// After the table, prints a compact one-line summary: total / done / overdue.
/// </summary>
[Description("List tasks (alias: ls).")]
public sealed class ListCommand(TaskManager manager) : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandOption("-s|--status <STATUS>")]
        [Description("Filter by: pending | inprogress | done | canceled | overdue | all (default)")]
        [DefaultValue("all")]
        public string Status { get; set; } = "all";

        [CommandOption("-t|--tag <TAG>")]
        [Description("Show only tasks that contain this tag.")]
        public string? Tag { get; set; }

        [CommandOption("--sort <FIELD>")]
        [Description("Sort by: created (default) | due | priority | title | status")]
        [DefaultValue("created")]
        public string Sort { get; set; } = "created";

        [CommandOption("--detail")]
        [Description("When set, render a full detail panel for each task instead of a table row.")]
        [DefaultValue(false)]
        public bool Detail { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        // ── Map --status string → filter values ───────────────────────────────
        TodoStatus? statusFilter     = null;
        bool        includeOverdue   = false;

        switch (settings.Status.ToLowerInvariant())
        {
            case "all":
                break;   // no filter
            case "overdue":
                // The "overdue" pseudo-status selects Pending/InProgress tasks
                // whose DueDate has passed; we pass null filter + includeOverdue.
                includeOverdue = true;
                break;
            case "pending":
                statusFilter = TodoStatus.Pending;
                break;
            case "inprogress" or "in-progress" or "started":
                statusFilter = TodoStatus.InProgress;
                break;
            case "done" or "complete" or "completed":
                statusFilter = TodoStatus.Done;
                break;
            case "canceled" or "cancelled":
                statusFilter = TodoStatus.Canceled;
                break;
            default:
                TaskRenderer.Error($"Unknown status filter '{settings.Status}'. " +
                                   "Use: all, pending, inprogress, done, canceled, overdue.");
                return 1;
        }

        // Special overdue-only mode: filter to tasks that IsOverdue == true.
        List<Models.TodoTask> tasks;
        if (settings.Status.Equals("overdue", StringComparison.OrdinalIgnoreCase))
        {
            var all = await manager.GetAllAsync();
            tasks = all.Where(t => t.IsOverdue).ToList();
        }
        else
        {
            tasks = await manager.GetFilteredAsync(
                filter:         statusFilter,
                includeOverdue: includeOverdue,
                tag:            settings.Tag,
                sortBy:         settings.Sort);
        }

        // ── Render ─────────────────────────────────────────────────────────────
        var tableTitle = BuildTitle(settings);

        if (settings.Detail)
        {
            foreach (var t in tasks)
                TaskRenderer.RenderTaskDetail(t);
        }
        else
        {
            TaskRenderer.RenderTaskTable(tasks, tableTitle);
        }

        // ── Summary line ───────────────────────────────────────────────────────
        var stats = await manager.GetStatsAsync();
        TaskRenderer.RenderListSummary(stats, tasks.Count);

        return 0;
    }

    private static string BuildTitle(Settings s)
    {
        var parts = new List<string> { "Tasks" };
        if (!s.Status.Equals("all", StringComparison.OrdinalIgnoreCase))
            parts.Add(s.Status);
        if (!string.IsNullOrWhiteSpace(s.Tag))
            parts.Add($"#{s.Tag}");
        if (!s.Sort.Equals("created", StringComparison.OrdinalIgnoreCase))
            parts.Add($"sorted by {s.Sort}");
        return string.Join(" · ", parts);
    }
}
