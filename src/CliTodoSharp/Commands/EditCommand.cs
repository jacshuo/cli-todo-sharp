using System.ComponentModel;
using System.Globalization;
using CliTodoSharp.Models;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo edit &lt;INDEX&gt; [--title "…"] [--priority high] [--due 2025-03-01] [--tags x,y]</c>
///
/// Patches any subset of a task's fields.
/// Only the options that are explicitly supplied are modified; the rest are left
/// unchanged.  This is a "partial update" (PATCH) semantic, not a full replace.
/// </summary>
[Description("Edit task fields.")]
public sealed class EditCommand(TaskManager manager) : AsyncCommand<EditCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "<INDEX>")]
        [Description("Display index of the task.")]
        public int Index { get; set; }

        [CommandOption("--title <TEXT>")]
        [Description("New title for the task.")]
        public string? Title { get; set; }

        [CommandOption("--description <TEXT>")]
        [Description("New description.")]
        public string? Description { get; set; }

        [CommandOption("-p|--priority <LEVEL>")]
        [Description("none | low | medium | high | critical")]
        public string? Priority { get; set; }

        [CommandOption("--due <DATE>")]
        [Description("New due date (yyyy-MM-dd). Use --clear-due to remove the due date.")]
        public string? DueDate { get; set; }

        [CommandOption("--clear-due")]
        [Description("Remove the due date entirely.")]
        [DefaultValue(false)]
        public bool ClearDue { get; set; }

        [CommandOption("--clear-description")]
        [Description("Remove the description entirely.")]
        [DefaultValue(false)]
        public bool ClearDescription { get; set; }

        [CommandOption("-t|--tags <TAGS>")]
        [Description("Replace all tags with this comma-separated list.")]
        public string? Tags { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        // ── Parse optional priority ────────────────────────────────────────────
        TaskPriority? priority = null;
        if (settings.Priority is not null)
        {
            if (!Enum.TryParse<TaskPriority>(settings.Priority, ignoreCase: true, out var p))
            {
                TaskRenderer.Error($"Unknown priority '{settings.Priority}'.");
                return 1;
            }
            priority = p;
        }

        // ── Parse optional due date ────────────────────────────────────────────
        DateTime? dueDate = null;
        if (!string.IsNullOrWhiteSpace(settings.DueDate))
        {
            if (!DateTime.TryParseExact(settings.DueDate,
                    ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm"],
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                TaskRenderer.Error($"Cannot parse date '{settings.DueDate}'.");
                return 1;
            }
            dueDate = parsed.ToUniversalTime();
        }

        // ── Parse optional tag replacement ─────────────────────────────────────
        IEnumerable<string>? tags = settings.Tags is null
            ? null
            : settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                       StringSplitOptions.TrimEntries);

        // ── Check at least one field is being changed ──────────────────────────
        if (settings.Title is null && settings.Description is null
            && priority is null && dueDate is null
            && !settings.ClearDue && !settings.ClearDescription && tags is null)
        {
            TaskRenderer.Warn("No fields to update. " +
                "Supply at least one of --title, --description, --clear-description, --priority, --due, --clear-due, --tags.");
            return 0;
        }

        try
        {
            var task = await manager.EditAsync(
                settings.Index,
                newTitle:          settings.Title,
                newDescription:    settings.Description,
                clearDescription:  settings.ClearDescription,
                newPriority:       priority,
                newDueDate:        dueDate,
                clearDueDate:      settings.ClearDue,
                newTags:           tags);

            TaskRenderer.Success($"Task [bold]#{task.DisplayIndex}[/] updated.");
            TaskRenderer.RenderTaskDetail(task);
            return 0;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            TaskRenderer.Error(ex.Message);
            return 1;
        }
    }
}
