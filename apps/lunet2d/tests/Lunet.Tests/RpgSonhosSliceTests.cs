using System.Numerics;
using Lunet;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Runtime;
using Lunet.Storage;

namespace Lunet.Tests;

public sealed class RpgSonhosSliceTests
{
    private static string Root()
    {
        DirectoryInfo? d = new(AppContext.BaseDirectory);
        for (int i = 0; i < 14 && d is not null; i++, d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "samples", "rpg-dos-sonhos");
            if (Directory.Exists(p)) return p;
        }
        throw new DirectoryNotFoundException("samples/rpg-dos-sonhos");
    }

    private static readonly Lazy<(byte[] Assembly, byte[]? Symbols)> Code = new(() =>
    {
        var sources = Directory.GetFiles(Root(), "*.cs", SearchOption.AllDirectories)
            .Select(p => new SourceFile(Path.GetRelativePath(Root(), p), File.ReadAllText(p))).ToList();
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly))
            .Compile("RpgSonhosSlice", sources);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return (result.Assembly!, result.Symbols);
    });

    private static LoadedGame Load() => GameLoader.Load(Code.Value.Assembly, Code.Value.Symbols);
    private static dynamic World(LoadedGame game, int seed = 20261030) =>
        Activator.CreateInstance(game.Game.GetType().Assembly.GetType("DreamWorld")!, seed)!;

    [Fact]
    public void SeedsRepeatAndAllEssentialPlacesRemainReachable()
    {
        using var game = Load();
        dynamic a = World(game, 12), b = World(game, 12), c = World(game, 13);
        Assert.Equal((string)a.Layout, (string)b.Layout);
        Assert.NotEqual((string)a.Layout, (string)c.Layout);
        for (int seed = 0; seed < 64; seed++)
        {
            dynamic w = World(game, seed);
            var queue = new Queue<(int X, int Y)>();
            var seen = new HashSet<(int X, int Y)> { (4, 18) };
            queue.Enqueue((4, 18));
            while (queue.TryDequeue(out var p))
                foreach (var delta in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = (X: p.X + delta.Item1, Y: p.Y + delta.Item2);
                    if (!(bool)w.IsBlocked(n.X, n.Y) && seen.Add(n)) queue.Enqueue(n);
                }
            foreach (var p in new[] { (4,18), (6,18), (6,19), (7,16), (8,16), (7,15), (8,15), (10,12), (19,5) })
                Assert.Contains(p, seen);
        }
    }

    [Fact]
    public void HarvestIsFiniteAndCookingHasAnObservableConsequence()
    {
        using var game = Load();
        dynamic w = World(game);
        w.State.X = 240f; w.State.Y = 528f;
        w.Step(0.016f, Vector2.Zero, false, true, false);
        Assert.Equal(1, (int)w.State.Berries);
        w.Step(0.016f, Vector2.Zero, false, true, false);
        Assert.Equal(1, (int)w.State.Berries); // same bush cannot be farmed
        w.State.X = 144f; w.State.Y = 592f;
        w.Step(0.016f, Vector2.Zero, false, true, false);
        Assert.Equal(0, (int)w.State.Berries);
        Assert.Equal(1, (int)w.State.Stock);
        for (int i = 0; i < 360; i++) w.Step(1f/60, Vector2.Zero, false, false, false);
        Assert.Equal(0, (int)w.State.Stock);
        Assert.Equal(1, (int)w.State.ReadyMeals);
        w.State.X = 144f; w.State.Y = 592f;
        w.Step(0.016f, Vector2.Zero, false, true, false);
        Assert.Equal(1, (int)w.State.Meals);
        w.State.Health = 40;
        w.Step(0.016f, Vector2.Zero, false, false, true);
        Assert.Equal(75, (int)w.State.Health);
        Assert.Equal(0, (int)w.State.Meals);
    }

    [Fact]
    public void ClueCanBeRecoveredFromIndependentSourcesAndDoesNotGateRuin()
    {
        using var game = Load();
        dynamic w = World(game);
        w.State.X = 208f; w.State.Y = 624f;
        w.Step(0.016f, Vector2.Zero, false, true, false);
        Assert.True((bool)w.State.ScoutClue);
        w.State.ScoutClue = false;
        w.State.X = 336f; w.State.Y = 400f;
        w.Step(0.016f, Vector2.Zero, false, true, false);
        Assert.True((bool)w.State.SignClue);
        Assert.Contains("ponte", (string)w.Message);
        w.State.SignClue = false;
        Assert.False((bool)w.IsBlocked(19, 5));
    }

    [Fact]
    public void TelegraphCanBeDodgedAndMeleeHasRangeAndCooldown()
    {
        using var game = Load();
        dynamic w = World(game);
        w.State.X = 624f; w.State.Y = 210f;
        w.Step(0.016f, Vector2.Zero, true, false, false);
        Assert.Equal(60, (int)w.State.EnemyHealth);
        w.Step(0.016f, Vector2.Zero, true, false, false);
        Assert.Equal(60, (int)w.State.EnemyHealth);
        for (int i=0; i<15; i++) w.Step(1f/60, Vector2.Zero, false, false, false);
        Assert.True((float)w.Windup > 0);
        int hp = (int)w.State.Health;
        for (int i=0; i<60; i++) w.Step(1f/60, new Vector2(-1,0), false, false, false);
        Assert.Equal(hp, (int)w.State.Health);
        w.State.X = 144f; w.State.Y = 592f;
        for (int i=0; i<40; i++) w.Step(1f/60, Vector2.Zero, false, false, false);
        w.Step(0.016f, Vector2.Zero, true, false, false);
        Assert.Equal(60, (int)w.State.EnemyHealth);
    }

    [Fact]
    public void VictoryRetreatAndSemanticSaveValidationAreRealRules()
    {
        using var game = Load();
        dynamic w = World(game);
        for(int hit=0; hit<3; hit++)
        {
            w.State.X=624f; w.State.Y=210f;
            w.Step(0.016f,Vector2.Zero,true,false,false);
            for(int i=0;i<40;i++) w.Step(1f/60,Vector2.Zero,false,false,false);
        }
        Assert.True((bool)w.State.RuinCleared);
        Assert.Equal(0,(int)w.State.EnemyHealth);
        Assert.Contains("não é um dos sete",(string)w.Message);
        dynamic vulnerable=World(game);
        vulnerable.State.X=624f; vulnerable.State.Y=210f; vulnerable.State.Health=1;
        vulnerable.State.Berries=2;
        for(int i=0;i<90;i++) vulnerable.Step(1f/60,Vector2.Zero,false,false,false);
        Assert.Equal(100,(int)vulnerable.State.Health);
        Assert.Equal(144f,(float)vulnerable.State.X);
        Assert.Equal(2,(int)vulnerable.State.Berries);
        vulnerable.State.X=float.NaN;
        Assert.False((bool)w.Restore(vulnerable.State));
    }

    [Fact]
    public void ActualLunetRuntimeRendersAcceptsTouchPausesAndRestoresDiskSave()
    {
        string directory = Path.Combine(Path.GetTempPath(), "rpg-sonhos-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DirectorySaveStore(directory);
            using (var game = Load())
            {
                var backend = new RecordingBackend();
                var host = new GameHost(game.Game, backend, saveStore: store);
                Assert.True(host.Start(480,800), host.Fault?.ToString());
                dynamic w = ((dynamic)game.Game).World;
                float x = (float)w.State.X;
                host.SetSurfaceTouches(new[] { new TouchPoint(1,TouchPhase.Pressed,new Vector2(100,700)) });
                host.Tick(1d/60);
                host.SetSurfaceTouches(new[] { new TouchPoint(1,TouchPhase.Moved,new Vector2(145,700)) });
                for (int i=0; i<20; i++) host.Tick(1d/60);
                Assert.True((float)w.State.X > x);
                host.SetSurfaceTouches(Array.Empty<TouchPoint>());
                host.Pause();
                float pausedX = (float)w.State.X;
                for (int i=0; i<30; i++) host.Tick(1d/60);
                Assert.Equal(pausedX, (float)w.State.X);
                Assert.True(store.Exists("rpg-slice-v1"));
                host.Resume();
                for (int i=0; i<20; i++) host.Tick(1d/60);
                Assert.Equal(pausedX, (float)w.State.X); // no stuck finger after resume
                Assert.False(host.IsFaulted, host.Fault?.ToString());
                Assert.NotEmpty(backend.Batches);
                host.Stop();
                Assert.Empty(backend.LiveTextures);
            }
            using (var loaded = Load())
            {
                var host = new GameHost(loaded.Game, new RecordingBackend(), saveStore: store);
                Assert.True(host.Start(480,800), host.Fault?.ToString());
                dynamic w = ((dynamic)loaded.Game).World;
                Assert.True((float)w.State.X > 144f);
                host.Stop();
            }
            store.WriteText("rpg-slice-v1", "{broken");
            using (var loaded = Load())
            {
                var host = new GameHost(loaded.Game, new RecordingBackend(), saveStore: store);
                Assert.True(host.Start(480,800), host.Fault?.ToString());
                Assert.Contains("inválido", (string)((dynamic)loaded.Game).World.Message);
                Assert.Equal("{broken", store.ReadText("rpg-slice-v1"));
                host.Stop(); // corrupt save must remain untouched until explicit recovery
                Assert.Equal("{broken", store.ReadText("rpg-slice-v1"));
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
