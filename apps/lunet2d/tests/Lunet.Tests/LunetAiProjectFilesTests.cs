using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class LunetAiProjectFilesTests
{
    [Fact]
    public void ReadAndWrite_RequireMatchingContentHash()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiSmoke");
            var files = new LunetAiProjectFiles(project);
            var initial = files.Read(project.Manifest.EntryPoint);
            Assert.Contains("class", initial.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(64, initial.Sha256.Length);
            Assert.Throws<InvalidOperationException>(() => files.Write(project.Manifest.EntryPoint, "// stale", "wrong"));
            Assert.Equal(initial.Content, project.ReadText(project.Manifest.EntryPoint));
            var changed = files.Write(project.Manifest.EntryPoint, initial.Content + "\n// ai\n", initial.Sha256);
            Assert.Equal(changed.Sha256, files.Read(project.Manifest.EntryPoint).Sha256);
            Assert.Contains("// ai", project.ReadText(project.Manifest.EntryPoint));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Write_DeniesTraversalManifestBinaryAndImplicitOverwrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiBoundaries");
            var files = new LunetAiProjectFiles(project);
            Assert.ThrowsAny<Exception>(() => files.Write("../escape.cs", "bad", ""));
            Assert.ThrowsAny<Exception>(() => files.Write("lunet.json", "{}", ""));
            Assert.ThrowsAny<Exception>(() => files.Write("Content/Textures/hero.png", "bad", ""));
            Assert.ThrowsAny<Exception>(() => files.Write(project.Manifest.EntryPoint, "bad", ""));
            Assert.ThrowsAny<Exception>(() => files.Read("Content/Textures/hero.png"));
            Assert.DoesNotContain("../escape.cs", files.List());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Write_CreatesTextFileAndNeverEscapesProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiAddFile");
            var files = new LunetAiProjectFiles(project);
            var change = files.Write("Content/Data/dialog.json", "{\"dialog\":[]}", "");
            Assert.Contains("Content/Data/dialog.json", files.List());
            Assert.Equal(change.Sha256, files.Read("Content/Data/dialog.json").Sha256);
            Assert.ThrowsAny<Exception>(() => files.Write("/tmp/leak.cs", "", ""));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
