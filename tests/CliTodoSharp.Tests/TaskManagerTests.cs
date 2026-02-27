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
}
