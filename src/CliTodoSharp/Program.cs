using CliTodoSharp.Commands;
using CliTodoSharp.Infrastructure;
using CliTodoSharp.Rendering;
using CliTodoSharp.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

// ── Determine the storage path from the first pass of the args ────────────────
// We look for "--storage <path>" manually here (before Spectre parses args) so
// we can inject the correctly-configured storage service into the DI container.
// All other args are left unchanged for Spectre to process normally.
string? storagePath = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--storage" or "-S")
    {
        storagePath = args[i + 1];
        break;
    }
}

// ── Build the DI container ────────────────────────────────────────────────────
var services = new ServiceCollection();

// Storage: singleton because JSON reads/writes are cheap and we want one shared
// lock object protecting the file across the entire command invocation.
services.AddSingleton<ITaskStorageService>(_ => new JsonTaskStorageService(storagePath));

// TaskManager depends on ITaskStorageService; register as singleton too.
services.AddSingleton<TaskManager>();

// Commands are registered as transients – Spectre creates one per invocation.
services.AddTransient<AddCommand>();
services.AddTransient<ListCommand>();
services.AddTransient<StartCommand>();
services.AddTransient<DoneCommand>();
services.AddTransient<CancelCommand>();
services.AddTransient<ReopenCommand>();
services.AddTransient<RemoveCommand>();
services.AddTransient<EditCommand>();
services.AddTransient<ShowCommand>();
services.AddTransient<StatsCommand>();
services.AddTransient<PurgeCommand>();

// ── Configure and run Spectre.Console.Cli ─────────────────────────────────────
var registrar = new TypeRegistrar(services);
var app       = new CommandApp(registrar);

app.Configure(config =>
{
    // Application-level metadata shown in the help text.
    config.SetApplicationName("todo");
    config.SetApplicationVersion("1.0.0");

    // Propagate exceptions so that the process exits with code 1 on errors.
    // In production you'd set this only in debug builds; here we surface
    // domain errors through TaskRenderer.Error instead, so this is benign.
    config.PropagateExceptions();

    // ── Command registrations ────────────────────────────────────────────────

    config.AddCommand<AddCommand>("add")
          .WithDescription("Add a new task.")
          .WithExample("todo", "add", "\"Buy milk\"", "--priority", "low", "--due", "2025-03-15");

    // "list" with alias "ls"
    config.AddCommand<ListCommand>("list")
          .WithDescription("List tasks.")
          .WithAlias("ls")
          .WithExample("todo", "list", "--status", "overdue")
          .WithExample("todo", "list", "--tag", "work", "--sort", "priority");

    config.AddCommand<ShowCommand>("show")
          .WithDescription("Show full detail of a task.")
          .WithExample("todo", "show", "3");

    config.AddCommand<StartCommand>("start")
          .WithDescription("Mark a task as In Progress.")
          .WithExample("todo", "start", "2");

    config.AddCommand<DoneCommand>("done")
          .WithDescription("Mark a task as Done.")
          .WithExample("todo", "done", "2");

    config.AddCommand<CancelCommand>("cancel")
          .WithDescription("Cancel (abandon) a task.")
          .WithExample("todo", "cancel", "5");

    config.AddCommand<ReopenCommand>("reopen")
          .WithDescription("Reopen a Done or Canceled task.")
          .WithExample("todo", "reopen", "5");

    config.AddCommand<RemoveCommand>("remove")
          .WithDescription("Permanently remove a task.")
          .WithAlias("rm")
          .WithExample("todo", "remove", "3", "--yes");

    config.AddCommand<EditCommand>("edit")
          .WithDescription("Edit task fields.")
          .WithExample("todo", "edit", "1", "--title", "\"New title\"", "--priority", "critical");

    config.AddCommand<StatsCommand>("stats")
          .WithDescription("Show task statistics with progress bars.");

    config.AddCommand<PurgeCommand>("purge")
          .WithDescription("Delete all Done and Canceled tasks.");
});

// ── Execute ───────────────────────────────────────────────────────────────────
try
{
    return await app.RunAsync(args);
}
catch (Exception ex)
{
    // Top-level handler: render domain/user errors with the styled renderer,
    // and re-throw unexpected exceptions so the stack trace is visible.
    TaskRenderer.Error(ex.Message);
    return 1;
}
