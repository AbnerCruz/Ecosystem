using Lunet;
using Lunet.Compiler;
using Lunet.Core;
using Lunet.Runtime;

namespace Lunet.Server;

/// <summary>
/// <c>lunet-server</c> (LUNET-502): compiles a Lunet project and runs it headless as a dedicated server.
/// Usage: <c>lunet-server &lt;pasta-do-projeto&gt; [--seconds N] [--check] [--saves pasta]</c>.
/// The game decides what a server does by checking <c>Game.IsHeadless</c> (e.g. <c>NetworkSession.Host(..., dedicated: true)</c>).
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("lunet-server <pasta-do-projeto> [--seconds N] [--check] [--saves pasta]");
            Console.WriteLine("  Compila o projeto Lunet e o executa sem tela, como servidor dedicado.");
            Console.WriteLine("  No jogo, use Game.IsHeadless para hospedar: NetworkSession.Host(..., dedicated: true).");
            return args.Length == 0 ? 2 : 0;
        }
        string directory = Path.GetFullPath(args[0]);
        double? seconds = Option(args, "--seconds") is { } s && double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        bool checkOnly = args.Contains("--check");
        string saves = Option(args, "--saves") ?? Path.Combine(directory, ".lunet", "server-saves");

        var result = ServerHost.Compile(directory, out var project);
        foreach (var diagnostic in result.Diagnostics) Console.Error.WriteLine(diagnostic);
        if (!result.Success) { Console.Error.WriteLine("Falha na compilação."); return 1; }
        Console.WriteLine($"{project.Name}: compilado ({result.Assembly!.Length / 1024} KiB).");
        if (checkOnly) return 0;

        using var runner = HeadlessGameRunner.Load(result.Assembly!, result.Symbols, Path.Combine(directory, "Content"), saves);
        runner.Game.Log.Written += (level, message) => Console.WriteLine($"[{level}] {message}");
        if (!runner.Start()) { Console.Error.WriteLine("O jogo falhou ao iniciar: " + runner.Host.Fault); return 3; }
        Console.WriteLine($"Servidor rodando a {runner.Game.Configuration.UpdatesPerSecond} atualizações/s. Ctrl+C para parar.");
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
        await runner.RunAsync(cancel.Token, seconds);
        if (runner.Host.IsFaulted) { Console.Error.WriteLine("O jogo parou com erro: " + runner.Host.Fault); return 3; }
        Console.WriteLine("Servidor encerrado.");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

/// <summary>Compilation of a project folder, shared by the CLI and its tests.</summary>
public static class ServerHost
{
    public static CompileResult Compile(string projectDirectory, out LunetProject project)
    {
        var full = Path.GetFullPath(projectDirectory);
        project = new ProjectStore(Path.GetDirectoryName(full)!).Open(Path.GetFileName(full));
        var sources = project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
        var references = new LoadedAssembliesReferenceProvider(typeof(Game).Assembly, typeof(Lunet.Networking.NetworkSession).Assembly);
        return new GameCompiler(references).Compile(project.Manifest.GameId.Replace('.', '_').Replace('-', '_'), sources);
    }
}
