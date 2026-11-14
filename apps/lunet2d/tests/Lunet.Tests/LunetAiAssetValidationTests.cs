using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class LunetAiAssetValidationTests
{
    [Fact]
    public void CompileAssetAudit_RejectsMissingTextureAndAcceptsPersistedSheet()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AssetAudit");
            project.WriteText("Game.cs", """
                using Lunet;
                public sealed class Example : Game
                {
                    protected override void LoadContent()
                    {
                        Content.LoadTexture("Content/Textures/Cells.png");
                    }
                }
                """);
            Assert.Contains("Cells.png", Assert.Single(LunetAiAssetValidation.Validate(project)));
            Directory.CreateDirectory(Path.Combine(project.Directory, "Content", "Textures"));
            File.WriteAllBytes(Path.Combine(project.Directory, "Content", "Textures", "Cells.png"),
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            Assert.Empty(LunetAiAssetValidation.Validate(project));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CompileAssetAudit_RejectsNonPngEvenIfFileExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("InvalidSheet");
            project.WriteText("Game.cs", "using Lunet; public sealed class G : Game { protected override void LoadContent() { Content.LoadTexture(\"Assets/hero.png\"); } }");
            Directory.CreateDirectory(Path.Combine(project.Directory, "Assets"));
            File.WriteAllText(Path.Combine(project.Directory, "Assets", "hero.png"), "not a png");
            Assert.Contains("assinatura PNG", Assert.Single(LunetAiAssetValidation.Validate(project)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
