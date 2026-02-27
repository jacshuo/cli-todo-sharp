using CliTodoSharp.Models;
using CliTodoSharp.Services;
using FluentAssertions;
using Xunit;

namespace CliTodoSharp.Tests;

/// <summary>
/// Unit tests for <see cref="TaskManager"/>.
///
/// The <see cref="InMemoryTaskStorage"/> stub replaces the real JSON storage so
/// tests run entirely in-memory – no file I/O, no flakiness from disk state.
/// </summary>
public sealed class TaskManagerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh <see cref="TaskManager"/> backed by the in-memory stub.
    /// Optionally pre-populates it with a set of tasks.
    /// </summary>
    private static (TaskManager mgr, InMemoryTaskStorage storage) Build(
        IEnumerable<TodoTask>? seed = null)
    {
        var storage = new InMemoryTaskStorage(seed);
        return (new TaskManager(storage), storage);
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsTask_WithCorrectFields()
    {
        var (mgr, _) = Build();

        var task = await mgr.AddAsync(
            "Buy milk",
            description: "From the corner shop",
            priority: TaskPriority.Low,
            dueDate: null,
            tags: ["shopping"]);

        task.Title.Should().Be("Buy milk");
        task.Description.Should().Be("From the corner shop");
        task.Priority.Should().Be(TaskPriority.Low);
        task.Status.Should().Be(TodoStatus.Pending);
        task.Tags.Should().ContainSingle(t => t == "shopping");
        task.Id.Should().NotBe(Guid.Empty);
        task.DisplayIndex.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_AssignsSequentialDisplayIndices()
    {
        var (mgr, _) = Build();

        await mgr.AddAsync("First");
        await mgr.AddAsync("Second");
        var all = await mgr.GetAllAsync();

        all.Select(t => t.DisplayIndex).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task AddAsync_TrimsAndLowercasesTags()
    {
        var (mgr, _) = Build();

        var task = await mgr.AddAsync("Task", tags: ["  Work ", "HOME"]);

        task.Tags.Should().BeEquivalentTo(["work", "home"]);
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_TransitionsPending_ToInProgress()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");

        var task = await mgr.StartAsync(1);

        task.Status.Should().Be(TodoStatus.InProgress);
        task.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_Throws_WhenIndexNotFound()
    {
        var (mgr, _) = Build();   // empty – no tasks

        var act = () => mgr.StartAsync(99);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task StartAsync_Throws_WhenAlreadyInProgress()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");
        await mgr.StartAsync(1);

        var act = () => mgr.StartAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already*");
    }

    [Fact]
    public async Task StartAsync_Throws_WhenDone()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");
        await mgr.CompleteAsync(1);

        var act = () => mgr.StartAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Complete ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteAsync_SetsStatusDone_AndRecordsTimestamp()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");

        var task = await mgr.CompleteAsync(1);

        task.Status.Should().Be(TodoStatus.Done);
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteAsync_WorksFromInProgress()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");
        await mgr.StartAsync(1);

        var task = await mgr.CompleteAsync(1);

        task.Status.Should().Be(TodoStatus.Done);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_SetsCanceled()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");

        var task = await mgr.CancelAsync(1);

        task.Status.Should().Be(TodoStatus.Canceled);
    }

    [Fact]
    public async Task CancelAsync_Throws_WhenAlreadyDone()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");
        await mgr.CompleteAsync(1);

        var act = () => mgr.CancelAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Reopen ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReopenAsync_ResetsDoneTask_ToPending()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");
        await mgr.CompleteAsync(1);

        var task = await mgr.ReopenAsync(1);

        task.Status.Should().Be(TodoStatus.Pending);
        task.CompletedAt.Should().BeNull();
        task.StartedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReopenAsync_Throws_WhenPending()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");

        var act = () => mgr.ReopenAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_DeletesTask()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task A");
        await mgr.AddAsync("Task B");

        await mgr.RemoveAsync(1);

        var all = await mgr.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Title.Should().Be("Task B");
    }

    [Fact]
    public async Task RemoveAsync_Throws_WhenIndexNotFound()
    {
        var (mgr, _) = Build();

        var act = () => mgr.RemoveAsync(99);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Purge ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PurgeAsync_RemovesDoneAndCanceled_LeavesOthers()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Keep – pending");
        await mgr.AddAsync("Remove – done");
        await mgr.CompleteAsync(2);
        await mgr.AddAsync("Keep – in progress");
        await mgr.StartAsync(3);
        await mgr.AddAsync("Remove – canceled");
        await mgr.CancelAsync(4);

        var removed = await mgr.PurgeAsync();

        removed.Should().Be(2);
        var remaining = await mgr.GetAllAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().OnlyContain(t =>
            t.Status == TodoStatus.Pending || t.Status == TodoStatus.InProgress);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("P1");
        await mgr.AddAsync("P2");
        await mgr.StartAsync(2);
        await mgr.AddAsync("P3");
        await mgr.CompleteAsync(3);
        await mgr.AddAsync("P4");
        await mgr.CancelAsync(4);

        var stats = await mgr.GetStatsAsync();

        stats.Total.Should().Be(4);
        stats.Pending.Should().Be(1);
        stats.InProgress.Should().Be(1);
        stats.Done.Should().Be(1);
        stats.Canceled.Should().Be(1);
    }

    [Fact]
    public async Task CompletionRate_IsZero_WhenNoTasksExist()
    {
        var (mgr, _) = Build();

        var stats = await mgr.GetStatsAsync();

        stats.Total.Should().Be(0);
        stats.CompletionRate.Should().Be(0);
    }

    [Fact]
    public async Task CompletionRate_IsZero_WhenAllTasksAreCanceled()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task A"); await mgr.CancelAsync(1);
        await mgr.AddAsync("Task B"); await mgr.CancelAsync(2);

        var stats = await mgr.GetStatsAsync();

        // denominator = total(2) - canceled(2) = 0  → rate = 0
        stats.CompletionRate.Should().Be(0);
    }

    [Fact]
    public async Task CompletionRate_ExcludesCanceledFromDenominator()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Done 1");  await mgr.CompleteAsync(1);
        await mgr.AddAsync("Done 2");  await mgr.CompleteAsync(2);
        await mgr.AddAsync("Pending");
        await mgr.AddAsync("Canceled"); await mgr.CancelAsync(4);

        // denominator = total(4) - canceled(1) = 3 ; done = 2
        var stats = await mgr.GetStatsAsync();
        stats.CompletionRate.Should().BeApproximately(2.0 / 3.0, precision: 0.001);
    }

    // ── Filter / sort ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFilteredAsync_ByTag_ReturnsOnlyMatchingTasks()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Work task", tags: ["work"]);
        await mgr.AddAsync("Home task", tags: ["home"]);
        await mgr.AddAsync("Both",      tags: ["work", "home"]);

        var work = await mgr.GetFilteredAsync(tag: "work");

        work.Should().HaveCount(2);
        work.Should().OnlyContain(t => t.Tags.Contains("work"));
    }

    [Fact]
    public async Task GetFilteredAsync_SortByPriority_OrdersDescending()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Low",      priority: TaskPriority.Low);
        await mgr.AddAsync("Critical", priority: TaskPriority.Critical);
        await mgr.AddAsync("Medium",   priority: TaskPriority.Medium);

        var sorted = await mgr.GetFilteredAsync(sortBy: "priority");

        sorted[0].Priority.Should().Be(TaskPriority.Critical);
        sorted[1].Priority.Should().Be(TaskPriority.Medium);
        sorted[2].Priority.Should().Be(TaskPriority.Low);
    }

    // ── GetFilteredAsync – additional sort/filter paths ───────────────────────

    [Fact]
    public async Task GetFilteredAsync_SortByDue_OrdersAscending()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Far",  dueDate: DateTime.UtcNow.AddDays(10).ToUniversalTime());
        await mgr.AddAsync("Near", dueDate: DateTime.UtcNow.AddDays(1).ToUniversalTime());
        await mgr.AddAsync("None");

        var sorted = await mgr.GetFilteredAsync(sortBy: "due");

        // Tasks with no due date sort last (DateTime.MaxValue sentinel)
        sorted[0].Title.Should().Be("Near");
        sorted[1].Title.Should().Be("Far");
        sorted[2].Title.Should().Be("None");
    }

    [Fact]
    public async Task GetFilteredAsync_SortByTitle_OrdersAlphabetically()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Banana");
        await mgr.AddAsync("apple");
        await mgr.AddAsync("Cherry");

        var sorted = await mgr.GetFilteredAsync(sortBy: "title");

        sorted.Select(t => t.Title.ToLower())
              .Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetFilteredAsync_SortByStatus_OrdersByStatusEnum()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("C"); await mgr.CancelAsync(1);
        await mgr.AddAsync("P");
        await mgr.AddAsync("D"); await mgr.CompleteAsync(3);

        var sorted = await mgr.GetFilteredAsync(sortBy: "status");

        // Status enum: Pending=0, InProgress=1, Done=2, Canceled=3
        sorted[0].Status.Should().Be(TodoStatus.Pending);
        sorted[1].Status.Should().Be(TodoStatus.Done);
        sorted[2].Status.Should().Be(TodoStatus.Canceled);
    }

    [Fact]
    public async Task GetFilteredAsync_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Pending 1");
        await mgr.AddAsync("Pending 2");
        await mgr.AddAsync("Done 1");   await mgr.CompleteAsync(3);

        var result = await mgr.GetFilteredAsync(filter: TodoStatus.Pending);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.Status == TodoStatus.Pending);
    }

    [Fact]
    public async Task GetFilteredAsync_IncludeOverdue_ReturnsOverduePendingTasks()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Overdue",  dueDate: DateTime.UtcNow.AddDays(-2));
        await mgr.AddAsync("On time",  dueDate: DateTime.UtcNow.AddDays(2));
        await mgr.AddAsync("Done old", dueDate: DateTime.UtcNow.AddDays(-1));
        await mgr.CompleteAsync(3);

        // Filter for Done + include any overdue tasks too
        var result = await mgr.GetFilteredAsync(
            filter: TodoStatus.Done,
            includeOverdue: true);

        // Should include the explicitly done task plus the overdue pending task
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Title == "Done old");
        result.Should().Contain(t => t.Title == "Overdue");
    }

    // ── GetByIndexAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIndexAsync_ReturnsTask_WhenFound()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Target task");

        var task = await mgr.GetByIndexAsync(1);

        task.Title.Should().Be("Target task");
        task.DisplayIndex.Should().Be(1);
    }

    [Fact]
    public async Task GetByIndexAsync_Throws_WhenIndexNotFound()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Only task");

        var act = () => mgr.GetByIndexAsync(99);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsTask_ByFullGuid()
    {
        var (mgr, storage) = Build();
        var added = await mgr.AddAsync("Find me");

        var found = await mgr.GetByIdAsync(added.Id.ToString());

        found.Id.Should().Be(added.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTask_ByPartialPrefix()
    {
        var (mgr, _) = Build();
        var added = await mgr.AddAsync("Prefix task");
        var prefix = added.Id.ToString()[..8];

        var found = await mgr.GetByIdAsync(prefix);

        found.Id.Should().Be(added.Id);
    }

    [Fact]
    public async Task GetByIdAsync_Throws_WhenNoMatch()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");

        var act = () => mgr.GetByIdAsync("00000000-0000-0000-0000-000000000000");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_Throws_WhenPrefixIsAmbiguous()
    {
        // Seed two tasks whose GUIDs share the same first character ('0'–'f'
        // collisions are unlikely with random UUIDs, so we craft them directly).
        var id1  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var id2  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        var seed = new[]
        {
            new TodoTask { Id = id1, Title = "Task 1" },
            new TodoTask { Id = id2, Title = "Task 2" },
        };
        var (mgr, _) = Build(seed);

        // Prefix shared by both
        var act = () => mgr.GetByIdAsync("aaaaaaaa");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Multiple*");
    }

    // ── EditAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EditAsync_UpdatesTitle()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Old title");

        var updated = await mgr.EditAsync(1, newTitle: "New title");

        updated.Title.Should().Be("New title");
    }

    [Fact]
    public async Task EditAsync_UpdatesDescription()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task", description: "original");

        var updated = await mgr.EditAsync(1, newDescription: "updated notes");

        updated.Description.Should().Be("updated notes");
    }

    [Fact]
    public async Task EditAsync_ClearsDescription()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task", description: "will be removed");

        var updated = await mgr.EditAsync(1, clearDescription: true);

        updated.Description.Should().BeNull();
    }

    [Fact]
    public async Task EditAsync_UpdatesPriority()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");

        var updated = await mgr.EditAsync(1, newPriority: TaskPriority.Critical);

        updated.Priority.Should().Be(TaskPriority.Critical);
    }

    [Fact]
    public async Task EditAsync_UpdatesDueDate()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task");
        var due = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var updated = await mgr.EditAsync(1, newDueDate: due);

        updated.DueDate.Should().BeCloseTo(due, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task EditAsync_ClearsDueDate()
    {
        var due = DateTime.UtcNow.AddDays(3);
        var (mgr, _) = Build();
        await mgr.AddAsync("Task", dueDate: due);

        var updated = await mgr.EditAsync(1, clearDueDate: true);

        updated.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task EditAsync_ReplacesTags()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("Task", tags: ["old"]);

        var updated = await mgr.EditAsync(1, newTags: ["new1", "new2"]);

        updated.Tags.Should().BeEquivalentTo(["new1", "new2"]);
        updated.Tags.Should().NotContain("old");
    }

    [Fact]
    public async Task EditAsync_Throws_WhenIndexNotFound()
    {
        var (mgr, _) = Build();

        var act = () => mgr.EditAsync(99, newTitle: "x");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── GetStatsAsync – overdue ───────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_CountsOverdue_Separately_ExcludesFromPending()
    {
        var (mgr, _) = Build();
        await mgr.AddAsync("On time");
        await mgr.AddAsync("Overdue", dueDate: DateTime.UtcNow.AddDays(-2));

        var stats = await mgr.GetStatsAsync();

        stats.Total.Should().Be(2);
        // The overdue task should NOT count as plain Pending
        stats.Pending.Should().Be(1);
        stats.Overdue.Should().Be(1);
    }
}
