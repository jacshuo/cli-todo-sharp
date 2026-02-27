using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo start &lt;INDEX&gt;</c>
/// Moves a Pending task to InProgress and shows its updated detail.
/// </summary>
[Description("Mark a task as In Progress.")]
public sealed class StartCommand(TaskManager manager) : AsyncCommand<StartCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "<INDEX>")]
        [Description("Display index of the task (see 'todo list').")]
        public int Index { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        try
        {
            var task = await manager.StartAsync(settings.Index);
            TaskRenderer.Success($"Task [bold]#{task.DisplayIndex}[/] is now [dodgerblue2]In Progress[/].");
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
