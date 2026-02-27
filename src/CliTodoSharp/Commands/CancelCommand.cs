using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo cancel &lt;INDEX&gt;</c>
/// Abandons a Pending or InProgress task.
/// </summary>
[Description("Cancel (abandon) a task.")]
public sealed class CancelCommand(TaskManager manager) : AsyncCommand<CancelCommand.Settings>
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
            var task = await manager.CancelAsync(settings.Index);
            TaskRenderer.Success($"Task [bold]#{task.DisplayIndex}[/] has been [grey42]Canceled[/].");
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
