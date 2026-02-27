using System.ComponentModel;
using System.Globalization;
using CliTodoSharp.Models;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo add "Buy groceries" --priority high --due 2025-12-31 --tags shopping,home</c>
///
/// Creates a new task and immediately prints its detail panel.
/// If the --title option is omitted and a positional argument is not provided,
/// an interactive prompt is shown (uses Spectre's TextPrompt).
/// </summary>
[Description("Add a new task.")]
public sealed class AddCommand(TaskManager manager) : AsyncCommand<AddCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "[TITLE]")]
        [Description("Short title of the task (prompted if omitted).")]
        public string? Title { get; set; }

        [CommandOption("-d|--description <TEXT>")]
        [Description("Optional longer description.")]
        public string? Description { get; set; }

        [CommandOption("-p|--priority <LEVEL>")]
        [Description("none | low | medium (default) | high | critical")]
        [DefaultValue("medium")]
        public string Priority { get; set; } = "medium";

        [CommandOption("--due <DATE>")]
        [Description("Due date in ISO format: yyyy-MM-dd or yyyy-MM-ddTHH:mm")]
        public string? DueDate { get; set; }

        [CommandOption("-t|--tags <TAGS>")]
        [Description("Comma-separated tags, e.g. work,home")]
        public string? Tags { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        // ── Resolve title (interactive prompt if not supplied) ─────────────────
        var title = settings.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            // TextPrompt renders a styled in-terminal prompt asking for input.
            title = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Task title:[/]")
                    .PromptStyle("dodgerblue2")
                    .ValidationErrorMessage("[red]Title cannot be empty.[/]")
                    .Validate(t => !string.IsNullOrWhiteSpace(t)));
        }

        // ── Parse priority ─────────────────────────────────────────────────────
        if (!Enum.TryParse<TaskPriority>(settings.Priority, ignoreCase: true, out var priority))
        {
            TaskRenderer.Error($"Unknown priority '{settings.Priority}'. " +
                               "Use: none, low, medium, high, critical.");
            return 1;
        }

        // ── Parse due date ─────────────────────────────────────────────────────
        DateTime? dueDate = null;
        if (!string.IsNullOrWhiteSpace(settings.DueDate))
        {
            // Try both date-only and date+time formats.
            if (!DateTime.TryParseExact(settings.DueDate,
                    ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm"],
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                TaskRenderer.Error($"Cannot parse date '{settings.DueDate}'. " +
                                   "Expected: yyyy-MM-dd or yyyy-MM-ddTHH:mm");
                return 1;
            }
            // Store as UTC so the file is timezone-independent.
            dueDate = parsed.ToUniversalTime();
        }

        // ── Parse tags ─────────────────────────────────────────────────────────
        var tags = string.IsNullOrWhiteSpace(settings.Tags)
            ? Enumerable.Empty<string>()
            : settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                      StringSplitOptions.TrimEntries);

        // ── Persist ────────────────────────────────────────────────────────────
        var task = await TaskRenderer.WithSpinner(
            "Adding task…",
            () => manager.AddAsync(title, settings.Description, priority, dueDate, tags));

        TaskRenderer.Success($"Task [bold]#{task.DisplayIndex}[/] created.");
        TaskRenderer.RenderTaskDetail(task);
        return 0;
    }
}
