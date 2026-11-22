using System.Text.Json;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>LRN-T01: contracts for ten real beginner projects, not ten placeholder cards.</summary>
public sealed class LearningTrackOneTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-t01-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FirstTrackHasFiveAvailableCoursesAndTenDistinctPlayableMissions()
    {
        Assert.Equal(60, LearningCatalog.Courses.Count);
        Assert.Equal(12, LearningCatalog.Tracks.Count);
        var courses = LearningCatalog.Courses.Where(c => c.Track == LearningCatalog.Tracks[0]).ToList();
        Assert.Equal(5, courses.Count);
        Assert.All(courses, c => Assert.True(c.Available, c.Id));

        var projects = LearningCatalog.Projects;
        Assert.Equal(10, projects.Count);
        Assert.Equal(10, projects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (var course in courses)
            Assert.Equal(2, projects.Count(p => p.CourseId == course.Id));

        Assert.All(projects, p =>
        {
            Assert.True(LearningCatalog.IsProjectAvailable(p.Id));
            Assert.Equal(p, LearningCatalog.FindProject(p.Id));
            Assert.Equal(4, p.Steps.Count);
            Assert.Equal(4, p.Steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count());
            Assert.All(p.Steps, step =>
            {
                Assert.StartsWith(p.Id + "-S", step.Id);
                Assert.False(string.IsNullOrWhiteSpace(step.Objective));
            });
            Assert.False(string.IsNullOrWhiteSpace(p.Mission));
            Assert.False(string.IsNullOrWhiteSpace(p.Investigate));
            Assert.False(string.IsNullOrWhiteSpace(p.Acceptance));
            Assert.False(string.IsNullOrWhiteSpace(p.Transfer));
            Assert.Contains(p.Orientation, new[] { "portrait", "landscape" });
        });
        Assert.All(LearningCatalog.Courses.Skip(5), c => Assert.False(c.Available));
        Assert.False(LearningCatalog.IsProjectAvailable("LRN-006-P1"));
        Assert.False(LearningCatalog.IsProjectAvailable("LRN-001-P3"));
        Assert.Null(LearningCatalog.FindProject("LRN-006-P1"));
        Assert.Equal(LearningCatalog.FirstProjectSteps, LearningCatalog.FindProject("LRN-001-P1")!.Steps);
    }

    [Fact]
    public void StepProgressAndLinksStayIndependentBetweenAllTenGames()
    {
        var path = Path.Combine(_root, "learning", "progress.json");
        var projectStore = new ProjectStore(Path.Combine(_root, "games"));
        var state = new LearningProgressStore(path);
        foreach (var (project, index) in LearningCatalog.Projects.Select((p, i) => (p, i)))
        {
            var game = projectStore.Create("Course" + (index + 1));
            var source = game.ReadText("Game.cs");
            state.LinkProject(project.Id, game.Name, game.Manifest.GameId);
            state.SetCompleted(project.Id, project.Steps[0].Id, true);
            Assert.Equal(source, game.ReadText("Game.cs"));
        }

        var reloaded = new LearningProgressStore(path);
        Assert.Equal(10, reloaded.OverallCompletedCount);
        foreach (var (project, index) in LearningCatalog.Projects.Select((p, i) => (p, i)))
        {
            Assert.Equal(1, reloaded.CompletedCountFor(project.Id));
            Assert.True(reloaded.Completed(project.Id, project.Steps[0].Id));
            Assert.False(reloaded.Completed(project.Id, project.Steps[1].Id));
            Assert.Equal("Course" + (index + 1), reloaded.ProjectNameFor(project.Id));
            Assert.Equal(projectStore.Open("Course" + (index + 1)).Manifest.GameId,
                reloaded.ProjectGameIdFor(project.Id));
        }
        var second = LearningCatalog.Projects[1];
        reloaded.SetCompleted(second.Id, second.Steps[0].Id, false);
        var changed = new LearningProgressStore(path);
        Assert.Equal(9, changed.OverallCompletedCount);
        Assert.Equal(1, changed.CompletedCountFor(LearningCatalog.FirstProjectId));
        Assert.Equal(0, changed.CompletedCountFor(second.Id));
    }

    [Fact]
    public void WorkspaceLearningPanelResolvesItsProjectByGameIdWithoutCrossingProjects()
    {
        var path = Path.Combine(_root, "progress.json");
        var projects = new ProjectStore(Path.Combine(_root, "games"));
        var a = projects.Create("Movement");
        var b = projects.Create("Collect");
        var state = new LearningProgressStore(path);
        Assert.Empty(state.LinkedProjectIdsForGameId(a.Manifest.GameId));
        state.LinkProject("LRN-001-P1", a.Name, a.Manifest.GameId);
        state.LinkProject("LRN-001-P2", a.Name, a.Manifest.GameId);
        state.LinkProject("LRN-002-P1", b.Name, b.Manifest.GameId);
        Assert.Equal(new[] { "LRN-001-P1", "LRN-001-P2" },
            state.LinkedProjectIdsForGameId(a.Manifest.GameId));
        Assert.Equal(new[] { "LRN-002-P1" }, state.LinkedProjectIdsForGameId(b.Manifest.GameId));
        Assert.Empty(state.LinkedProjectIdsForGameId("not-linked"));
        Assert.Equal(state.LinkedProjectIdsForGameId(a.Manifest.GameId),
            new LearningProgressStore(path).LinkedProjectIdsForGameId(a.Manifest.GameId));
    }

    [Fact]
    public void ExistingV1ProgressMigratesLosslesslyOnNextWrite()
    {
        var path = Path.Combine(_root, "progress.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Version = 1,
            ProjectName = "MeuJogo",
            ProjectGameId = "game-legacy",
            CompletedStepIds = new[] { "LRN-001-P1-S1", "LRN-001-P1-S4" }
        }));
        var state = new LearningProgressStore(path);
        Assert.Equal(2, state.CompletedCount);
        Assert.Equal("MeuJogo", state.ProjectName);
        Assert.Equal("game-legacy", state.ProjectGameId);
        var p2 = LearningCatalog.Projects[1];
        state.SetCompleted(p2.Id, p2.Steps[2].Id, true);
        var migrated = new LearningProgressStore(path);
        Assert.Equal(3, migrated.OverallCompletedCount);
        Assert.Equal(2, migrated.CompletedCountFor(LearningCatalog.FirstProjectId));
        Assert.Equal("MeuJogo", migrated.ProjectNameFor(LearningCatalog.FirstProjectId));
        Assert.Equal("game-legacy", migrated.ProjectGameIdFor(LearningCatalog.FirstProjectId));
        Assert.True(migrated.Completed(p2.Id, p2.Steps[2].Id));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(2, doc.RootElement.GetProperty("Version").GetInt32());
    }

    [Fact]
    public void RejectInvalidStepsAndKeepCorruptFilesIntact()
    {
        var path = Path.Combine(_root, "progress.json");
        var state = new LearningProgressStore(path);
        Assert.Throws<ArgumentException>(() => state.SetCompleted("LRN-001-P1", "LRN-002-P1-S1", true));
        Assert.Throws<ArgumentException>(() => state.SetCompleted("LRN-007-P1", "LRN-007-P1-S1", true));
        Assert.Throws<ArgumentException>(() => state.LinkProject("LRN-001-P1", "../invalid", "abc"));
        Directory.CreateDirectory(_root);
        var invalid = "{\"Version\":2,\"Projects\":[{\"Id\":\"LRN-001-P1\",\"Name\":null,\"GameId\":null,\"CompletedStepIds\":[\"LRN-001-P1-S1\",\"LRN-001-P1-S1\"]}]}";
        File.WriteAllText(path, invalid);
        Assert.Throws<InvalidDataException>(() => new LearningProgressStore(path));
        Assert.Equal(invalid, File.ReadAllText(path));
    }
}
