using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo desc &lt;INDEX&gt; [TEXT]</c>
///
/// A focused command for reading or writing the long-form description of a task.
///
/// Usage patterns:
///   todo desc 2                      – Print the current description.
///   todo desc 2 "Detailed notes…"   – Set / replace the description.
///   todo desc 2 --clear              – Remove the description entirely.
/// </summary>
[Description("View or set the description of a task.")]
public sealed class DescCommand(TaskManager manager) : AsyncCommand<DescCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "<INDEX>")]
        [Description("Display index of the task.")]
        public int Index { get; set; }

        /// <summary>
        /// The new description text.  Optional – omit to read the current value.
        /// </summary>
        [CommandArgument(1, "[TEXT]")]
        [Description("New description text.  Omit to print the current description.")]
        public string? Text { get; set; }

        [CommandOption("--clear")]
        [Description("Remove the description entirely.")]
        [DefaultValue(false)]
        public bool Clear { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        // ── Conflict guard ─────────────────────────────────────────────────────
        if (settings.Text is not null && settings.Clear)
        {
            TaskRenderer.Error("Cannot use both TEXT and --clear at the same time.");
            return 1;
        }

        try
        {
            // ── Read-only mode: no arguments – just display the description ────
            if (settings.Text is null && !settings.Clear)
            {
                var task = await manager.GetByIndexAsync(settings.Index);

                if (string.IsNullOrWhiteSpace(task.Description))
                {
                    AnsiConsole.MarkupLine($"[grey]Task [bold]#{task.DisplayIndex}[/] has no description.[/]");
                }
                else
                {
                    AnsiConsole.Write(new Spectre.Console.Rule(
                        $"[bold] Task #{task.DisplayIndex} – Description [/]")
                        .RuleStyle("grey42"));
                    AnsiConsole.MarkupLine(Spectre.Console.Markup.Escape(task.Description));
                    AnsiConsole.Write(new Spectre.Console.Rule().RuleStyle("grey42"));
                }
                return 0;
            }

            // ── Write mode: update or clear ────────────────────────────────────
            var updated = await manager.EditAsync(
                settings.Index,
                newDescription:   settings.Clear ? null : settings.Text,
                clearDescription: settings.Clear);

            if (settings.Clear)
                TaskRenderer.Success($"Description cleared from task [bold]#{updated.DisplayIndex}[/].");
            else
                TaskRenderer.Success($"Description updated on task [bold]#{updated.DisplayIndex}[/].");

            return 0;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            TaskRenderer.Error(ex.Message);
            return 1;
        }
    }
}
