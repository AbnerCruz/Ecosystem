using System.Diagnostics;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Storage;

namespace Lunet.Runtime;

/// <summary>
/// Runs a compiled Lunet game without a screen (LUNET-502; SPEC §8 "servidor .NET headless"): same game code, same
/// fixed-step loop, a <see cref="HeadlessGraphicsBackend"/>, content from the project folder and saves on disk. Portable
/// .NET: Windows, Linux and macOS servers, CI and tests.
/// </summary>
public sealed class HeadlessGameRunner : IDisposable
{
    private readonly LoadedGame _loaded;

    private HeadlessGameRunner(LoadedGame loaded, GameHost host, HeadlessGraphicsBackend graphics)
    {
        _loaded = loaded;
        Host = host;
        Graphics = graphics;
    }

    public GameHost Host { get; }
    public Game Game => _loaded.Game;
    public HeadlessGraphicsBackend Graphics { get; }

    /// <param name="contentDirectory">The project's <c>Content/</c> folder (missing = no content).</param>
    /// <param name="saveDirectory">Where <c>Save</c> writes (null = memory only).</param>
    public static HeadlessGameRunner Load(byte[] assembly, byte[]? symbols, string? contentDirectory, string? saveDirectory = null)
    {
        var loaded = GameLoader.Load(assembly, symbols);
        var graphics = new HeadlessGraphicsBackend();
        IContentSource? content = contentDirectory is not null && Directory.Exists(contentDirectory) ? new DirectoryContentSource(contentDirectory) : null;
        ISaveStore saves = saveDirectory is null ? new MemorySaveStore() : new DirectorySaveStore(saveDirectory);
        var host = new GameHost(loaded.Game, graphics, content, audio: null, saveStore: saves);
        return new HeadlessGameRunner(loaded, host, graphics);
    }

    /// <summary>Initializes and loads content. False (with <see cref="GameHost.Fault"/>) when the game threw.</summary>
    public bool Start() => Host.Start(Game.Configuration.VirtualWidth, Game.Configuration.VirtualHeight);

    /// <summary>Advances game time by <paramref name="seconds"/> (simulated, as fast as the CPU allows).</summary>
    public void Advance(double seconds, double frame = 1 / 60.0)
    {
        for (double t = 0; t < seconds && !Host.IsFaulted; t += frame) Host.Tick(frame);
    }

    /// <summary>Real-time loop until cancelled or the game faults; sleeps between steps so an idle server stays cool.</summary>
    public async Task RunAsync(CancellationToken cancellationToken, double? maxSeconds = null)
    {
        var step = 1.0 / Math.Max(1, Game.Configuration.UpdatesPerSecond);
        var clock = Stopwatch.StartNew();
        double last = 0;
        while (!cancellationToken.IsCancellationRequested && !Host.IsFaulted && (maxSeconds is null || clock.Elapsed.TotalSeconds < maxSeconds))
        {
            double now = clock.Elapsed.TotalSeconds;
            Host.Tick(now - last);
            last = now;
            var wait = step - (clock.Elapsed.TotalSeconds - now);
            if (wait > 0.001)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(wait), cancellationToken); }
                catch (TaskCanceledException) { break; }
            }
        }
    }

    public void Dispose()
    {
        Host.Stop();
        _loaded.Dispose();
    }
}
