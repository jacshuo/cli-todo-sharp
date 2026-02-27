using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo done &lt;INDEX&gt;</c>
/// Marks a task as Done (from Pending or InProgress).
/// </summary>
[Description("Mark a task as Done.")]
public sealed class DoneCommand(TaskManager manager) : AsyncCommand<DoneCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "<INDEX>")]
        [Description("Display index of the task.")]
        public int Index { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        try
        {
            var task = await manager.CompleteAsync(settings.Index);
            TaskRenderer.Success($"Task [bold]#{task.DisplayIndex}[/] marked as [green3]Done[/].");
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
