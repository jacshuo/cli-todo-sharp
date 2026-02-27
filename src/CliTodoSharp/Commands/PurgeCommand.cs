using System.ComponentModel;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// <c>todo purge</c>
/// Bulk-deletes all Done and Canceled tasks to keep the list tidy.
/// Requires explicit confirmation unless --yes is passed.
/// </summary>
[Description("Delete all Done and Canceled tasks.")]
public sealed class PurgeCommand(TaskManager manager) : AsyncCommand<PurgeCommand.Settings>
{
    public sealed class Settings : BaseCommandSettings
    {
        [CommandOption("-y|--yes")]
        [Description("Skip the confirmation prompt.")]
        [DefaultValue(false)]
        public bool Yes { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext ctx, Settings settings)
    {
        if (!settings.Yes)
        {
            var ok = AnsiConsole.Confirm(
                "[yellow3]Delete ALL Done and Canceled tasks?[/] This cannot be undone.",
                defaultValue: false);
            if (!ok) { AnsiConsole.MarkupLine("[grey42]Aborted.[/]"); return 0; }
        }

        var count = await manager.PurgeAsync();
        TaskRenderer.Success($"Purged [bold]{count}[/] task(s).");
        return 0;
    }
}
