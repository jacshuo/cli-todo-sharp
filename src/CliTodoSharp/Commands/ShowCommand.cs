using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo show &lt;INDEX&gt;</c>
/// Renders the full detail panel for a single task.
/// </summary>
[Description("Show full detail of a task.")]
public sealed class ShowCommand(TaskManager manager) : AsyncCommand<ShowCommand.Settings>
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
            var task = await manager.GetByIndexAsync(settings.Index);
            TaskRenderer.RenderTaskDetail(task);
            return 0;
        }
        catch (KeyNotFoundException ex)
        {
            TaskRenderer.Error(ex.Message);
            return 1;
        }
    }
}
