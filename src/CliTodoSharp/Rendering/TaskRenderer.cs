using CliTodoSharp.Models;
using CliTodoSharp.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CliTodoSharp.Rendering;

/// <summary>
/// Central rendering hub that converts domain objects into rich Spectre.Console
/// output: coloured tables, progress bars, status badges, and info panels.
///
/// Design choices:
/// • All methods are static so commands can call them without a separate DI
///   registration; they only need the parsed domain data.
/// • "Markup" strings use Spectre Console's square-bracket colour/style syntax,
///   e.g. [bold red]text[/].  Special characters like '[' and ']' in raw user
///   data must be escaped with <see cref="Markup.Escape"/> before embedding.
/// </summary>
public static class TaskRenderer
{
    // ── Colour palette ────────────────────────────────────────────────────────

    // Using named constants makes it easy to retheme the whole app in one place.
    private const string ColPending    = "grey70";
    private const string ColInProgress = "dodgerblue2";
    private const string ColDone       = "green3";
    private const string ColCanceled   = "grey42";
    private const string ColOverdue    = "red1";

    private const string ColCritical   = "bold red1";
    private const string ColHigh       = "darkorange";
    private const string ColMedium     = "yellow3";
    private const string ColLow        = "steelblue1";
    private const string ColNone       = "grey50";

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Render a full list of tasks as a rich table.
    /// Columns: #  │  Title  │  Status  │  Priority  │  Due  │  Tags
    /// </summary>
    public static void RenderTaskTable(IEnumerable<TodoTask> tasks, string? title = null)
    {
        var taskList = tasks.ToList();

        var table = new Table
        {
            Border      = TableBorder.Rounded,
            // Expand to fill the terminal width for a clean look.
            Expand      = true,
        };

        if (title is not null)
            table.Title = new TableTitle($"[bold]{Markup.Escape(title)}[/]");

        // ── Column definitions ────────────────────────────────────────────────
        table.AddColumn(new TableColumn("[bold grey70]#[/]")
            .RightAligned().Width(4));
        table.AddColumn(new TableColumn("[bold]Title[/]"));
        table.AddColumn(new TableColumn("[bold]Status[/]").Centered().Width(12));
        table.AddColumn(new TableColumn("[bold]Priority[/]").Centered().Width(10));
        table.AddColumn(new TableColumn("[bold]Due[/]").Centered().Width(13));
        table.AddColumn(new TableColumn("[bold]Tags[/]").Width(16));

        if (taskList.Count == 0)
        {
            // Span all columns to show a friendly empty state.
            table.AddRow(
                new Markup(""),
                new Markup("[grey]No tasks found.[/]"),
                new Markup(""),
                new Markup(""),
                new Markup(""),
                new Markup("")
            );
        }
        else
        {
            foreach (var t in taskList)
                table.AddRow(BuildTaskRow(t));
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Render a detailed single-task panel with all fields.
    /// </summary>
    public static void RenderTaskDetail(TodoTask task)
    {
        // Build the content grid (label + value pairs)
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().Width(14));
        grid.AddColumn(new GridColumn());

        void Row(string label, string value) =>
            grid.AddRow($"[grey70]{label}[/]", value);

        Row("ID",          $"[dim]{task.Id}[/]");
        Row("Title",       $"[bold]{Markup.Escape(task.Title)}[/]");
        Row("Status",      StatusMarkup(task));
        Row("Priority",    PriorityMarkup(task.Priority));
        Row("Due",         DueDateMarkup(task.DueDate));
        Row("Created",     FormatLocalTime(task.CreatedAt));
        Row("Updated",     FormatLocalTime(task.UpdatedAt));

        if (task.StartedAt.HasValue)
            Row("Started",  FormatLocalTime(task.StartedAt.Value));
        if (task.CompletedAt.HasValue)
            Row("Completed",FormatLocalTime(task.CompletedAt.Value));
        if (task.Tags.Count > 0)
            Row("Tags",    string.Join(" ", task.Tags.Select(tg => $"[dim]#{tg}[/]")));
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            grid.AddRow("", "");   // blank separator
            grid.AddRow("[grey70]Description[/]", Markup.Escape(task.Description!));
        }

        var panel = new Panel(grid)
        {
            Header  = new PanelHeader($"[bold] Task #{task.DisplayIndex} [/]"),
            Border  = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
        };

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Render statistics with progress bars – one bar per status group plus an
    /// overall completion rate bar.  Uses Spectre's BreakdownChart for the
    /// proportional segment chart and ProgressColumn logic for the info bars.
    /// </summary>
    public static void RenderStats(TaskStats stats, string storagePath)
    {
        // ── Header panel ─────────────────────────────────────────────────────
        AnsiConsole.Write(new Rule("[bold]Task Statistics[/]").RuleStyle("grey42"));

        // ── Segment breakdown chart ───────────────────────────────────────────
        // BreakdownChart renders a horizontal bar split into coloured segments.
        if (stats.Total > 0)
        {
            var chart = new BreakdownChart().FullSize();

            if (stats.Pending    > 0) chart.AddItem("Pending",    stats.Pending,    Color.Grey70);
            if (stats.InProgress > 0) chart.AddItem("In Progress",stats.InProgress, Color.DodgerBlue2);
            if (stats.Done       > 0) chart.AddItem("Done",       stats.Done,       Color.Green3);
            if (stats.Canceled   > 0) chart.AddItem("Canceled",   stats.Canceled,   Color.Grey42);
            if (stats.Overdue    > 0) chart.AddItem("Overdue",    stats.Overdue,    Color.Red1);

            AnsiConsole.Write(chart);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]No tasks yet.[/]");
        }

        AnsiConsole.WriteLine();

        // ── Progress bars (per-status) ────────────────────────────────────────
        // We simulate a Spectre progress display using a table of bar cells
        // because the live ProgressContext is designed for async work, not static
        // summary rendering.  Each "bar" is a simple proportional fill.

        var barTable = new Table().NoBorder().Expand();
        barTable.AddColumn(new TableColumn("").Width(14).NoWrap());
        barTable.AddColumn(new TableColumn(""));   // progress bar cell – expands naturally
        barTable.AddColumn(new TableColumn("").Width(6).RightAligned().NoWrap());

        void AddBar(string label, int count, string colour)
        {
            if (stats.Total == 0) return;
            double frac = (double)count / stats.Total;
            barTable.AddRow(
                $"[{colour}]{label}[/]",
                BuildProgressBarMarkup(frac, colour),
                $"[bold]{count}[/]"
            );
        }

        AddBar("Pending",    stats.Pending,    ColPending);
        AddBar("In Progress",stats.InProgress, ColInProgress);
        AddBar("Overdue",    stats.Overdue,    ColOverdue);
        AddBar("Done",       stats.Done,       ColDone);
        AddBar("Canceled",   stats.Canceled,   ColCanceled);

        AnsiConsole.Write(barTable);
        AnsiConsole.WriteLine();

        // ── Completion rate progress bar ──────────────────────────────────────
        int pct = (int)Math.Round(stats.CompletionRate * 100);
        AnsiConsole.MarkupLine(
            $"[bold]Overall completion:[/]  " +
            $"{BuildProgressBarMarkup(stats.CompletionRate, ColDone)}  [bold]{pct}%[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Storage: {Markup.Escape(storagePath)}[/]");
        AnsiConsole.Write(new Rule().RuleStyle("grey42"));
    }

    /// <summary>
    /// Show a spinner with an async operation running underneath.
    /// Wraps Spectre's <see cref="AnsiConsole.Status"/> to keep spinner logic
    /// in one place and out of command code.
    /// </summary>
    public static async Task<T> WithSpinner<T>(string message, Func<Task<T>> work)
    {
        T result = default!;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("dodgerblue2"))
            .StartAsync(message, async _ => { result = await work(); });
        return result;
    }

    /// <summary>Print a formatted success notice.</summary>
    public static void Success(string msg) =>
        AnsiConsole.MarkupLine($"[green3]✓[/] {msg}");

    /// <summary>Print a formatted error notice.</summary>
    public static void Error(string msg) =>
        AnsiConsole.MarkupLine($"[red1]✗[/] {msg}");

    /// <summary>Print a formatted warning notice.</summary>
    public static void Warn(string msg) =>
        AnsiConsole.MarkupLine($"[yellow3]⚠[/]  {msg}");

    /// <summary>
    /// Compact one-line summary printed below a task table:
    /// e.g.  Showing 4 of 9 tasks  ·  ✓ 2 done  ·  ⚠ 1 overdue
    /// </summary>
    public static void RenderListSummary(TaskStats stats, int shownCount)
    {
        var parts = new List<string>
        {
            $"[grey70]Showing [bold]{shownCount}[/] of [bold]{stats.Total}[/] task(s)[/]",
        };

        if (stats.Done       > 0) parts.Add($"[green3]✓ {stats.Done} done[/]");
        if (stats.InProgress > 0) parts.Add($"[dodgerblue2]⟳ {stats.InProgress} in progress[/]");
        if (stats.Overdue    > 0) parts.Add($"[red1]⚠ {stats.Overdue} overdue[/]");
        if (stats.Canceled   > 0) parts.Add($"[grey42]✕ {stats.Canceled} canceled[/]");

        AnsiConsole.MarkupLine(string.Join("  [grey42]·[/]  ", parts));
    }

    // ── Private row/cell builders ─────────────────────────────────────────────

    /// <summary>Build one table row (array of <see cref="IRenderable"/>) for a task.</summary>
    private static IRenderable[] BuildTaskRow(TodoTask t)
    {
        // Title: strikethrough for done/canceled tasks to communicate completion.
        var titleStyle = t.Status switch
        {
            TodoStatus.Done     => $"[{ColDone} strikethrough]",
            TodoStatus.Canceled => $"[{ColCanceled} strikethrough]",
            _ when t.IsOverdue  => $"[{ColOverdue}]",
            _                   => "[default]",
        };

        return
        [
            new Markup($"[grey70]{t.DisplayIndex}[/]"),
            new Markup($"{titleStyle}{Markup.Escape(t.Title)}[/]"),
            new Markup(StatusMarkup(t)),
            new Markup(PriorityMarkup(t.Priority)),
            new Markup(DueDateMarkup(t.DueDate)),
            new Markup(TagsMarkup(t.Tags)),
        ];
    }

    // ── Markup fragments ──────────────────────────────────────────────────────

    private static string StatusMarkup(TodoTask t)
    {
        if (t.IsOverdue)
            return $"[bold {ColOverdue}]⚠ Overdue[/]";

        return t.Status switch
        {
            TodoStatus.Pending    => $"[{ColPending}]◯ Pending[/]",
            TodoStatus.InProgress => $"[{ColInProgress}]⟳ In Progress[/]",
            TodoStatus.Done       => $"[{ColDone}]✓ Done[/]",
            TodoStatus.Canceled   => $"[{ColCanceled}]✕ Canceled[/]",
            _                     => t.Status.ToString(),
        };
    }

    private static string PriorityMarkup(TaskPriority p) => p switch
    {
        TaskPriority.Critical => $"[{ColCritical}]● Critical[/]",
        TaskPriority.High     => $"[{ColHigh}]● High[/]",
        TaskPriority.Medium   => $"[{ColMedium}]● Medium[/]",
        TaskPriority.Low      => $"[{ColLow}]● Low[/]",
        _                     => $"[{ColNone}]○ None[/]",
    };

    private static string DueDateMarkup(DateTime? due)
    {
        if (!due.HasValue) return "[grey42]—[/]";

        var local = due.Value.ToLocalTime();
        var diff  = local.Date - DateTime.Now.Date;

        var colour = diff.TotalDays switch
        {
            < 0           => ColOverdue,          // past
            0             => "bold orangered1",   // today
            <= 2          => "darkorange",         // soon
            _             => "grey70",
        };

        var label = diff.TotalDays switch
        {
            < 0  => $"{-diff.Days}d ago",
            0    => "today",
            1    => "tomorrow",
            <= 7 => $"in {diff.Days}d",
            _    => local.ToString("MMM d"),
        };

        return $"[{colour}]{Markup.Escape(label)}[/]";
    }

    private static string TagsMarkup(List<string> tags)
    {
        if (tags.Count == 0) return "[grey42]—[/]";
        return string.Join(" ", tags.Take(3).Select(tg => $"[dim]#{Markup.Escape(tg)}[/]"))
               + (tags.Count > 3 ? $"[grey42] +{tags.Count - 3}[/]" : "");
    }

    /// <summary>
    /// Build an ASCII progress bar as a Markup string.
    /// The bar is 30 characters wide by default.
    /// Example:  [████████████░░░░░░░░░░░░░░░░░░]  40%
    ///
    /// Technique: we calculate the filled count from the fraction and build
    /// a plain string, then wrap it in the appropriate Spectre colour markup.
    /// </summary>
    private static string BuildProgressBarMarkup(double fraction, string colour, int width = 30)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        int filled = (int)Math.Round(fraction * width);
        int empty  = width - filled;

        var bar = new string('█', filled) + new string('░', empty);
        return $"[{colour}]{Markup.Escape(bar)}[/]";
    }

    // ── Date helpers ──────────────────────────────────────────────────────────

    /// <summary>Format a UTC timestamp in local wall-clock time for display.</summary>
    private static string FormatLocalTime(DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
