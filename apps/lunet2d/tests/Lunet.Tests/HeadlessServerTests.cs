using System.Numerics;
using Lunet.Core;
using Lunet.Networking;
using Lunet.Runtime;
using Lunet.Server;

namespace Lunet.Tests;

/// <summary>LUNET-502: a Lunet project runs headless as a dedicated multiplayer server on any desktop OS.</summary>
public sealed class HeadlessServerTests
{
    private const string ServerGame = """
        using System.Collections.Generic;
        using System.Numerics;
        using Lunet;
        using Lunet.Networking;

        public sealed class Bandeira : NetworkObject
        {
            public readonly NetworkVariable<Vector2> Posicao = new();
            public readonly NetworkVariable<int> Ticks = new();
            protected override IEnumerable<NetworkVariableBase> Variables => [Posicao, Ticks];
        }

        public sealed class Servidor : Game
        {
            private NetworkSession? _rede;
            private Bandeira? _bandeira;
            protected override void Initialize()
            {
                Log.Info("headless=" + IsHeadless);
                if (!IsHeadless) return;
                _rede = NetworkSession.Host(new UdpTransport("bandeira"), 0, new NetworkSessionOptions { GameId = "bandeira" }, dedicated: true);
                _rede.Replication.Register("bandeira", () => new Bandeira());
                _bandeira = _rede.Replication.Spawn<Bandeira>("bandeira");
                Log.Info("porta=" + _rede.Port);
            }
            protected override void Update(GameTime time)
            {
                if (_rede is null || _bandeira is null) return;
                _bandeira.Ticks.Value++;
                _bandeira.Posicao.Value = new Vector2(_bandeira.Ticks.Value, 0);
                _rede.Update();
            }
        }
        """;

    private sealed class Bandeira : NetworkObject
    {
        public readonly NetworkVariable<Vector2> Posicao = new();
        public readonly NetworkVariable<int> Ticks = new();
        protected override IEnumerable<NetworkVariableBase> Variables => [Posicao, Ticks];
    }

    [Fact]
    public void HeadlessRunner_RunsTheGameWithoutAScreen_AndTheGameKnowsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-headless-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("Simples");
            var result = ServerHost.Compile(project.Directory, out _);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            using var runner = HeadlessGameRunner.Load(result.Assembly!, result.Symbols, Path.Combine(project.Directory, "Content"));
            Assert.True(runner.Start(), runner.Host.Fault?.ToString());
            Assert.True(runner.Game.IsHeadless);
            runner.Advance(1.0);
            Assert.False(runner.Host.IsFaulted);
            Assert.True(runner.Graphics.DrawCalls >= 0);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DedicatedServer_FromAProjectFolder_ReplicatesToARealUdpClient()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-server-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("Bandeira");
            project.WriteText(project.Manifest.EntryPoint, ServerGame);
            var result = ServerHost.Compile(project.Directory, out _);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            using var runner = HeadlessGameRunner.Load(result.Assembly!, result.Symbols, null);
            var log = new List<string>();
            runner.Game.Log.Written += (_, message) => log.Add(message);
            Assert.True(runner.Start(), runner.Host.Fault?.ToString());
            Assert.Contains("headless=True", log);
            int port = int.Parse(log.Single(l => l.StartsWith("porta=", StringComparison.Ordinal))["porta=".Length..]);

            using var client = NetworkSession.Join(new UdpTransport("bandeira"), "127.0.0.1", port, new NetworkSessionOptions { GameId = "bandeira" });
            client.Replication.Register("bandeira", () => new Bandeira());
            var deadline = DateTime.UtcNow.AddSeconds(8);
            Bandeira? seen = null;
            while (DateTime.UtcNow < deadline && (seen is null || seen.Ticks.Value < 30))
            {
                runner.Host.Tick(1 / 60.0);
                client.Update();
                seen = client.Replication.All<Bandeira>().FirstOrDefault();
                await Task.Delay(5, TestContext.Current.CancellationToken);
            }
            Assert.NotNull(seen);
            Assert.True(seen!.Ticks.Value >= 30, "ticks: " + seen.Ticks.Value);
            Assert.Equal(seen.Ticks.Value, (int)seen.Posicao.Value.X);
            Assert.False(runner.Host.IsFaulted, runner.Host.Fault?.ToString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cli_ChecksAndRunsAProjectForAFewSeconds()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-cli-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("Cli");
            Assert.Equal(0, await Program.Main([project.Directory, "--check"]));
            Assert.Equal(0, await Program.Main([project.Directory, "--seconds", "0.3"]));
            project.WriteText(project.Manifest.EntryPoint, "isto não é C#");
            Assert.Equal(1, await Program.Main([project.Directory, "--check"]));
            Assert.Equal(2, await Program.Main([]));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
