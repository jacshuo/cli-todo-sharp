using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo reopen &lt;INDEX&gt;</c>
/// Resets a Done or Canceled task back to Pending so it can be worked on again.
/// </summary>
[Description("Reopen a Done or Canceled task.")]
public sealed class ReopenCommand(TaskManager manager) : AsyncCommand<ReopenCommand.Settings>
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
            var task = await manager.ReopenAsync(settings.Index);
            TaskRenderer.Success($"Task [bold]#{task.DisplayIndex}[/] reopened as [grey70]Pending[/].");
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
