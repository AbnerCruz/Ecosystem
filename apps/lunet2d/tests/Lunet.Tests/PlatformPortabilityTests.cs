using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Lunet.Tests;

/// <summary>
/// Fase A da expansão multiplataforma (LUNET-492, ADR 0012 proposto): fixa o que já é verdade hoje — as camadas
/// portáteis não dependem de Android nem de qualquer outra plataforma — para que desktop, navegador e iOS possam
/// reutilizá-las sem reescrita. Só o host Android pode conhecer APIs da plataforma.
/// </summary>
public class PlatformPortabilityTests
{
    private static readonly string Src = FindSrc();

    private static readonly string[] PortableProjects =
    [
        "Lunet.Framework", "Lunet.Runtime", "Lunet.Compiler", "Lunet.Core", "Lunet.Studio",
        "Lunet.Editor", "Lunet.Docs", "Lunet.Git", "Lunet.Networking", "Lunet.Ai",
    ];

    private static readonly Regex PlatformApi = new(
        @"^\s*(global\s+)?using\s+(static\s+)?(Android|Java|Javax|Xamarin|AndroidX|UIKit|Foundation|CoreGraphics|ObjCRuntime|Microsoft\.Maui|Avalonia|System\.Windows|Microsoft\.UI)\b",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static string FindSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src");
    }

    public static IEnumerable<object[]> Projects() => PortableProjects.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Projects))]
    public void PortableProjectsTargetPlainNet(string project)
    {
        var doc = XDocument.Load(Path.Combine(Src, project, project + ".csproj"));
        var frameworks = doc.Descendants("TargetFramework").Concat(doc.Descendants("TargetFrameworks")).Select(e => e.Value).ToArray();
        Assert.NotEmpty(frameworks);
        Assert.All(frameworks, f => Assert.Matches(@"^net\d+\.\d+$", f)); // sem -android, -ios, -windows, -browser
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void PortableProjectsNeverUsePlatformApis(string project)
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(Src, project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => PlatformApi.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(Src, f))
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void RenderingAndAudioAreReachedOnlyThroughFrameworkContracts()
    {
        // As costuras já existentes que os novos hosts implementam (OpenGL ES no Android, WebGL/Metal/desktop depois).
        var framework = typeof(Lunet.Graphics.IGraphicsBackend).Assembly;
        Assert.True(framework.GetType("Lunet.Graphics.IGraphicsBackend")!.IsInterface);
        Assert.Contains(framework.GetTypes(), t => t.IsInterface && t.Name == "IAudioBackend");
        // A implementação concreta fica só no host Android.
        Assert.True(File.Exists(Path.Combine(Src, "Lunet.Android", "Gles", "GlesBackend.cs")));
        Assert.DoesNotContain(framework.GetTypes(), t => t.Name.Contains("Gles", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyTheAndroidHostTargetsAPlatformFramework()
    {
        var platformProjects = Directory.EnumerateFiles(Src, "*.csproj", SearchOption.AllDirectories)
            .Where(p => XDocument.Load(p).Descendants("TargetFramework").Any(e => Regex.IsMatch(e.Value, @"-(android|ios|windows|browser|macos|maccatalyst)")))
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();
        Assert.Equal(["Lunet.Android"], platformProjects);
    }
}
