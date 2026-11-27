using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lunet.Client;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// H4/M7 parity in the shell's AI panel: attachments from the game's Notes, image perception on every head, the
/// Framework docs and web research only when the settings allow it (the previous Android screen had all four).
/// </summary>
public sealed class AiCoworkShellTests
{
    private sealed class Recording : AgentRuntime.IModelProvider
    {
        public readonly List<AgentRuntime.ModelRequest> Requests = [];
        public string ProviderId => "chat-completions";
        public ValueTask<AgentRuntime.ModelResponse> CompleteAsync(AgentRuntime.ModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(new AgentRuntime.ModelResponse([new AgentRuntime.TextBlock("ok")], AgentRuntime.StopReason.EndTurn,
                new AgentRuntime.Usage(1, 1, AgentRuntime.Money.Zero("USD"))));
        }
    }

    private sealed class Vault : Lunet.Ai.ISecretVault
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Get(string name) => _values.GetValueOrDefault(name);
        public void Set(string name, string value) => _values[name] = value;
        public void Delete(string name) => _values.Remove(name);
    }

    private sealed class NoWeb : Lunet.Ai.ILunetAiWeb
    {
        public Task<IReadOnlyList<Lunet.Ai.LunetAiWebHit>> SearchAsync(string query, int count, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Lunet.Ai.LunetAiWebHit>>([]);
        public Task<string> FetchAsync(Uri url, int startLine, int maxLines, CancellationToken cancellationToken) => Task.FromResult("");
    }

    private static string Everything(AgentRuntime.ModelRequest r) =>
        r.System + "\n" + string.Join("\n", r.Messages.SelectMany(m => m.Content).OfType<AgentRuntime.TextBlock>().Select(t => t.Text));

    private static void RunToEnd(Task run)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!run.IsCompleted && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        Assert.True(run.IsCompleted);
        run.GetAwaiter().GetResult();
    }

    [Fact]
    public void AnAttachedNote_ReachesTheModel_WithImagePerception_AndWebOnlyWhenAllowed()
    {
        var root = Directory.CreateTempSubdirectory("lunet-cowork-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var model = new Recording();
                var aiDir = Path.Combine(root, ".ai");
                var store = new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "s.json"), new Vault());
                var knowledgeLoads = 0;
                var host = new AiHost(store, aiDir, model, Knowledge: () => { knowledgeLoads++; return null; }, Web: new NoWeb());
                var shell = new LunetShell(root) { AiHost = host };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var project = shell.Workspace.Project!;
                Directory.CreateDirectory(Path.Combine(project.Directory, "Notes"));
                File.WriteAllText(Path.Combine(project.Directory, "Notes", "ideias.md"), "# Ideias\nInimigo que pula.");
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var ai = shell.WorkspaceView.Support.Ai;

                Assert.False(ai.Attach("Notes/nao-existe.md"));                       // only files that exist in the game
                Assert.True(ai.Attach("Notes/ideias.md"));
                Assert.False(ai.Attach("Notes/ideias.md"));                           // no duplicates
                ai.Input.Text = "use a nota anexada";
                RunToEnd(ai.SendAsync());
                var first = model.Requests[0];
                Assert.Contains("Notes/ideias.md", Everything(first));
                Assert.Contains(first.Tools, t => t.Name == Lunet.Ai.LunetAiImageTool.Name); // perception on this head too
                Assert.Contains(first.Tools, t => t.Name == Lunet.Ai.LunetAiWebTool.SearchName);  // research on by default
                Assert.Empty(ai.Attachments);                                         // attachments belong to one message
                Assert.Equal(1, knowledgeLoads);

                store.Save(store.Load().Settings with { WebResearch = false }, null);    // turned off in the settings
                model.Requests.Clear();
                ai.Input.Text = "pesquise sobre pulo duplo";
                RunToEnd(ai.SendAsync());
                Assert.DoesNotContain(model.Requests[0].Tools, t => t.Name.StartsWith("lunet.web.", StringComparison.Ordinal));
                Assert.Equal(1, knowledgeLoads);                                      // indexed once, not per message
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TheAvaloniaDecoder_GivesStraightRgba_AndBoundsBigImages()
    {
        var dir = Directory.CreateTempSubdirectory("lunet-img-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                string Save(int w, int h, string name)
                {
                    using var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                    using (var fb = bmp.Lock())
                    {
                        var row = new byte[w * 4];
                        for (var y = 0; y < h; y++)
                        {
                            for (var x = 0; x < w; x++) { row[x * 4] = 200; row[x * 4 + 1] = 30; row[x * 4 + 2] = 10; row[x * 4 + 3] = (byte)(x == 0 ? 128 : 255); }
                            System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
                        }
                    }
                    var path = Path.Combine(dir, name);
#pragma warning disable CS0618 // the PNG default encoder is all this fixture needs
                    bmp.Save(path);
#pragma warning restore CS0618
                    return path;
                }
                var small = AvaloniaAiImages.Instance.Load(Save(4, 3, "a.png"), "a.png")!;
                Assert.Equal((4, 3), (small.Width, small.Height));
                Assert.Equal(new byte[] { 200, 30, 10, 255 }, small.Rgba[4..8]);          // opaque pixel: exact, RGBA order
                Assert.Equal(128, small.Rgba[3]);                                       // alpha kept
                Assert.InRange(small.Rgba[0], 198, 202);                               // un-premultiplied back to ~200
                var big = AvaloniaAiImages.Instance.Load(Save(1200, 600, "b.png"), "b.png")!;
                Assert.Equal(AvaloniaAiImages.MaxSide, big.Width);
                Assert.Equal(big.Width * big.Height * 4, big.Rgba.Length);
                File.WriteAllText(Path.Combine(dir, "c.png"), "não é imagem");
                Assert.Null(AvaloniaAiImages.Instance.Load(Path.Combine(dir, "c.png"), "c.png")); // unreadable ≠ crash
            });
        }
        finally { Directory.Delete(dir, true); }
    }
}
