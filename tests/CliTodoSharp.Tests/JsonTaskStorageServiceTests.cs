using CliTodoSharp.Models;
using CliTodoSharp.Services;
using FluentAssertions;
using Xunit;

namespace CliTodoSharp.Tests;

/// <summary>
/// Integration-style tests for <see cref="JsonTaskStorageService"/>.
/// Each test uses an isolated temp file that is deleted on teardown.
/// </summary>
public sealed class JsonTaskStorageServiceTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(
        Path.GetTempPath(), $"todo-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoad_RoundTrips_AllFields()
    {
        var svc = new JsonTaskStorageService(_tempFile);
        var due = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var original = new TodoTask
        {
            Title       = "Test task",
            Description = "Some detail",
            Status      = TodoStatus.InProgress,
            Priority    = TaskPriority.High,
            DueDate     = due,
            Tags        = ["work", "test"],
            StartedAt   = DateTime.UtcNow,
        };

        await svc.SaveAsync([original]);
        var loaded = await svc.LoadAsync();

        loaded.Should().HaveCount(1);
        var t = loaded[0];
        t.Id.Should().Be(original.Id);
        t.Title.Should().Be("Test task");
        t.Description.Should().Be("Some detail");
        t.Status.Should().Be(TodoStatus.InProgress);
        t.Priority.Should().Be(TaskPriority.High);
        t.DueDate.Should().BeCloseTo(due, TimeSpan.FromSeconds(1));
        t.Tags.Should().BeEquivalentTo(["work", "test"]);
        t.StartedAt.Should().NotBeNull();
    }

    // ── Empty file / first run ────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var svc = new JsonTaskStorageService(_tempFile);

        var result = await svc.LoadAsync();

        result.Should().BeEmpty();
    }

    // ── Multiple tasks ────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoad_PreservesMultipleTasks_WithCorrectIndices()
    {
        var svc  = new JsonTaskStorageService(_tempFile);
        var base_ = DateTime.UtcNow;

        var tasks = Enumerable.Range(1, 5).Select(i => new TodoTask
        {
            Title     = $"Task {i}",
            CreatedAt = base_.AddMinutes(i),
        }).ToList();

        await svc.SaveAsync(tasks);
        var loaded = await svc.LoadAsync();

        loaded.Should().HaveCount(5);
        loaded.Select(t => t.DisplayIndex).Should().BeEquivalentTo([1, 2, 3, 4, 5]);
        // Ordering must be stable (by CreatedAt)
        loaded.Select(t => t.Title)
              .Should().ContainInOrder("Task 1", "Task 2", "Task 3", "Task 4", "Task 5");
    }

    // ── Atomic write ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTemporaryFile()
    {
        var svc = new JsonTaskStorageService(_tempFile);
        await svc.SaveAsync([new TodoTask { Title = "x" }]);

        File.Exists(_tempFile + ".tmp").Should().BeFalse();
    }

    // ── Corrupt JSON ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_Throws_OnCorruptJson()
    {
        await File.WriteAllTextAsync(_tempFile, "{ this is not valid json }}}");
        var svc = new JsonTaskStorageService(_tempFile);

        var act = () => svc.LoadAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*parse*");
    }

    // ── Enum serialisation ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_WritesEnums_AsStrings_NotIntegers()
    {
        var svc = new JsonTaskStorageService(_tempFile);
        await svc.SaveAsync([new TodoTask
        {
            Title    = "t",
            Status   = TodoStatus.Done,
            Priority = TaskPriority.Critical,
        }]);

        var json = await File.ReadAllTextAsync(_tempFile);
        json.Should().Contain("\"done\"");
        json.Should().Contain("\"critical\"");
        // Must NOT contain raw integers for the enum values
        json.Should().NotMatchRegex("\"status\"\\s*:\\s*[0-9]");
    }

    // ── StoragePath ───────────────────────────────────────────────────────────

    [Fact]
    public void StoragePath_ReturnsAbsolutePath_WhenRelativePathGiven()
    {
        var rel = "relative/tasks.json";
        var svc = new JsonTaskStorageService(rel);

        svc.StoragePath.Should().Be(Path.GetFullPath(rel));
    }
}
