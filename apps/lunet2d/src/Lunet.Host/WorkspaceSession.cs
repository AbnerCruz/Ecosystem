using System.Diagnostics;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Input;
using Lunet.Runtime;

namespace Lunet.Host;

/// <summary>One compiled-and-running game inside a non-Android host (LUNET-482/493): CPU-rendered frames and input.</summary>
public sealed class RunningGame : IDisposable
{
    private readonly LoadedGame _loaded;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _last;
    private readonly Queue<double> _frameTimes = new();

    internal RunningGame(LoadedGame loaded, GameHost host, SoftwareGraphicsBackend screen)
    {
        _loaded = loaded; Host = host; Screen = screen;
    }

    public GameHost Host { get; }
    public SoftwareGraphicsBackend Screen { get; }
    public Game Game => _loaded.Game;
    /// <summary>Average CPU time of the last 60 frames (update + draw + rasterization), in milliseconds.</summary>
    public double AverageFrameMilliseconds => _frameTimes.Count == 0 ? 0 : _frameTimes.Average();

    /// <summary>Advances by the real time since the last call (or by <paramref name="seconds"/>) and renders; false when the game faulted.</summary>
    public bool Frame(double? seconds = null)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var started = Stopwatch.GetTimestamp();
        Host.Tick(seconds ?? Math.Min(0.1, now - _last));
        _last = now;
        _frameTimes.Enqueue(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        if (_frameTimes.Count > 60) _frameTimes.Dequeue();
        return !Host.IsFaulted;
    }

    /// <summary>Mouse or touch at a point of the surface (pixels, top-left origin); pointer id 0.</summary>
    public void Pointer(TouchPhase phase, float x, float y) => Host.SetSurfaceTouches([new TouchPoint(0, phase, new System.Numerics.Vector2(x, y))]);
    public void PointerUp() => Host.SetSurfaceTouches([]);
    public void Key(Keys key, bool down) => Host.Input.SetKey(key, down);

    public void Dispose()
    {
        Host.Stop();
        _loaded.Dispose();
    }
}

/// <summary>
/// A projects folder opened by a non-Android host (desktop first, DEC-0062): list, open, create, edit, compile and run,
/// using only the portable libraries (Core, Compiler, Runtime). The Android host keeps its own UI; this is the shared logic.
/// </summary>
public sealed class WorkspaceSession
{
    public WorkspaceSession(string projectsRoot)
    {
        Directory.CreateDirectory(projectsRoot);
        Store = new ProjectStore(projectsRoot);
        Root = projectsRoot;
    }

    public string Root { get; }
    public ProjectStore Store { get; }
    public LunetProject? Project { get; private set; }
    public CompileResult? LastCompile { get; private set; }

    public IReadOnlyList<string> Projects() =>
        Directory.EnumerateDirectories(Root).Where(d => File.Exists(Path.Combine(d, ProjectStore.ManifestFileName)))
            .Select(Path.GetFileName).OfType<string>().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public LunetProject Open(string name) => Project = Store.Open(name);
    public LunetProject Create(string name, string orientation = "landscape") => Project = Store.Create(name, ProjectTemplate.Blank, orientation);

    private LunetProject Require() => Project ?? throw new InvalidOperationException("Abra um projeto primeiro.");

    public IReadOnlyList<string> Files() => Require().ListFiles();
    public string Read(string path) => Require().ReadText(path);
    public void Write(string path, string text) => Require().WriteText(path, text);

    public CompileResult Compile()
    {
        var project = Require();
        var references = new LoadedAssembliesReferenceProvider(typeof(Game).Assembly, typeof(Lunet.Networking.NetworkSession).Assembly);
        var sources = project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
        return LastCompile = new GameCompiler(references).Compile("desktop_" + project.Manifest.GameId.Replace('.', '_').Replace('-', '_'), sources);
    }

    /// <summary>Compiles and starts the game with a CPU renderer of the given surface size; null when compilation fails.</summary>
    public RunningGame? Run(int surfaceWidth, int surfaceHeight)
    {
        var project = Require();
        var result = Compile();
        if (!result.Success) return null;
        var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var screen = new SoftwareGraphicsBackend(surfaceWidth, surfaceHeight);
        var saves = Path.Combine(project.Directory, ".lunet", "desktop-saves");
        var host = new GameHost(loaded.Game, screen, new ProjectContentSource(project.Directory), null, new Lunet.Storage.DirectorySaveStore(saves));
        var running = new RunningGame(loaded, host, screen);
        if (!host.Start(surfaceWidth, surfaceHeight)) { running.Dispose(); throw new InvalidOperationException("O jogo falhou ao iniciar: " + host.Fault?.Message, host.Fault); }
        return running;
    }
}
