using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo remove &lt;INDEX&gt;</c>  (alias: <c>todo rm</c>)
/// Permanently deletes a task.  Asks for confirmation unless --yes is passed.
/// </summary>
[Description("Permanently remove a task (alias: rm).")]
public sealed class RemoveCommand(TaskManager manager) : AsyncCommand<RemoveCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "<INDEX>")]
        [Description("Display index of the task.")]
        public int Index { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Skip the confirmation prompt.")]
        [DefaultValue(false)]
        public bool Yes { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        // Look up the task first so we can show its title in the prompt.
        Models.TodoTask task;
        try { task = await manager.GetByIndexAsync(settings.Index); }
        catch (KeyNotFoundException ex) { TaskRenderer.Error(ex.Message); return 1; }

        // Confirmation guard – prevents accidental deletions.
        if (!settings.Yes)
        {
            var confirmed = AnsiConsole.Confirm(
                $"Delete task [bold]#{task.DisplayIndex}[/] [grey70]\"{Markup.Escape(task.Title)}\"[/]?",
                defaultValue: false);

            if (!confirmed)
            {
                AnsiConsole.MarkupLine("[grey42]Aborted.[/]");
                return 0;
            }
        }

        try
        {
            await manager.RemoveAsync(settings.Index);
            TaskRenderer.Success(
                $"Task [bold]#{task.DisplayIndex}[/] [grey70]\"{Markup.Escape(task.Title)}\"[/] removed.");
            return 0;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            TaskRenderer.Error(ex.Message);
            return 1;
        }
    }
}
