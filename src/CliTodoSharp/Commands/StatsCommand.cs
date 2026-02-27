using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo stats</c>
///
/// Displays an at-a-glance dashboard with:
///   • A proportional breakdown segment chart (each status gets a colour block)
///   • Individual percentage-fill progress bars per status
///   • Overall completion rate bar
///   • Current storage file path
/// </summary>
[Description("Show task statistics with progress bars.")]
public sealed class StatsCommand(TaskManager manager, ITaskStorageService storage)
    : AsyncCommand<StatsCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        var stats = await manager.GetStatsAsync();
        TaskRenderer.RenderStats(stats, storage.StoragePath);
        return 0;
    }
}
