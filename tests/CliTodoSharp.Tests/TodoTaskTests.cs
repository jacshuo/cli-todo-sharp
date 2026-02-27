using CliTodoSharp.Models;
using FluentAssertions;
using Xunit;

namespace CliTodoSharp.Tests;

/// <summary>
/// Unit tests for computed properties on <see cref="TodoTask"/>,
/// particularly the <see cref="TodoTask.IsOverdue"/> end-of-day logic
/// introduced to avoid marking today's tasks as overdue before midnight.
/// </summary>
public sealed class TodoTaskTests
{
    // ── IsOverdue helpers ─────────────────────────────────────────────────────

    private static DateTime LocalEndOfToday()
        => DateTime.Now.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999 today

    private static DateTime LocalStartOfToday()
        => DateTime.Now.Date;

    // ── Due today ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_False_WhenDueDateIsToday_Pending()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.Pending,
            // Due at the very start of today (local) – still today, not overdue
            DueDate = LocalStartOfToday().ToUniversalTime(),
        };

        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_False_WhenDueDateIsEndOfToday_Pending()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.Pending,
            DueDate = LocalEndOfToday().ToUniversalTime(),
        };

        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_False_WhenDueDateIsToday_InProgress()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.InProgress,
            DueDate = LocalStartOfToday().ToUniversalTime(),
        };

        task.IsOverdue.Should().BeFalse();
    }

    // ── Due yesterday ─────────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_True_WhenDueDateWasYesterday_Pending()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.Pending,
            DueDate = DateTime.Now.Date.AddDays(-1).ToUniversalTime(),
        };

        task.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_True_WhenDueDateWasYesterday_InProgress()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.InProgress,
            DueDate = DateTime.Now.Date.AddDays(-1).ToUniversalTime(),
        };

        task.IsOverdue.Should().BeTrue();
    }

    // ── Finished tasks are never overdue ─────────────────────────────────────

    [Fact]
    public void IsOverdue_False_WhenDone_EvenIfDueDatePassed()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.Done,
            DueDate = DateTime.UtcNow.AddDays(-7),
        };

        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_False_WhenCanceled_EvenIfDueDatePassed()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.Canceled,
            DueDate = DateTime.UtcNow.AddDays(-7),
        };

        task.IsOverdue.Should().BeFalse();
    }

    // ── No due date ───────────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_False_WhenNoDueDate()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.Pending,
            DueDate = null,
        };

        task.IsOverdue.Should().BeFalse();
    }

    // ── Future due date ───────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_False_WhenDueDateIsInFuture()
    {
        var task = new TodoTask
        {
            Status  = TodoStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(5),
        };

        task.IsOverdue.Should().BeFalse();
    }
}
