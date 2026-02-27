using System.ComponentModel;
using CliTodoSharp.Services;
using Spectre.Console.Cli;

namespace CliTodoSharp.Commands;

/// <summary>
/// Shared settings inherited by every command.
/// The <c>--storage</c> option lets users point at a non-default JSON file,
/// which enables portable task files (e.g. on a USB drive or shared folder).
/// </summary>
public abstract class BaseCommandSettings : CommandSettings
{
    [CommandOption("--storage <PATH>")]
    [Description("Override the path to the tasks JSON file for this invocation.")]
    [DefaultValue(null)]
    public string? StoragePath { get; set; }
}
