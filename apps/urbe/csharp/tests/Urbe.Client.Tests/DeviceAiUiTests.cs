using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>"IA no aparelho" (24/10/2026): the offline model offered, downloaded with progress, chosen and used as a provider.</summary>
public sealed partial class HeadlessUiTests
{
    private sealed class FakeLocalAi : ILocalTextRuntime, IPartialDownloads
    {
        public static readonly LocalModel Small = new("pequena", "Modelo Pequeno", "Leve.", "p.gguf", "https://x.test/p", 3_000_000_000, new string('a', 64), 16384, 4000, true);
        public static readonly LocalModel Big = new("grande", "Modelo Grande", "O melhor.", "g.gguf", "https://x.test/g", 5_000_000_000, new string('b', 64), 16384, 9000, true);
        private readonly HashSet<string> _installed = [];
        public IReadOnlyList<LocalModel> Models => [Big, Small];
        public LocalModel Recommended => Small;
        public bool IsInstalled(LocalModel model) => _installed.Contains(model.Id);
        public long PartialBytes(LocalModel model) => 0;
        public TaskCompletionSource Gate { get; } = new();
        public async Task InstallAsync(LocalModel model, IProgress<(long Done, long Total)>? progress, CancellationToken cancel)
        {
            progress?.Report((model.Bytes / 4, model.Bytes));
            await Gate.Task.WaitAsync(cancel);
            progress?.Report((model.Bytes, model.Bytes));
            _installed.Add(model.Id);
        }
        public void Remove(LocalModel model) => _installed.Remove(model.Id);
        public List<AiRequest> Asked { get; } = [];
        public Task<AiResult> ChatAsync(LocalModel model, AiRequest request, Action<AiStreamEvent> onEvent, CancellationToken cancel)
        {
            Asked.Add(request);
            onEvent(new AiStreamEvent("text", "Olá do aparelho."));
            return Task.FromResult(new AiResult([AiBlock.TextBlock("Olá do aparelho.")], "", "end", new AiUsage(10, 4, null), model.Id));
        }
    }

    [Fact]
    public async Task Assistant_TheOfflineModelIsOfferedDownloadedAndUsed() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-device-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var before = UrbeApp.AssistantFolder;
            var runtime = new FakeLocalAi();
            var previous = LocalAi.Text;
            LocalAi.Text = runtime;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(storage);
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.AssistantLauncher.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                var panel = view.Assistant;
                var texts = () => panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
                // the offline AI comes first, before the providers that need a key
                Assert.Contains("IA no aparelho", texts());
                ClickButtonWithText(panel, "IA no aparelho");
                Assert.Equal("IA no aparelho", panel.ViewTitle);
                Assert.Contains("Modelo Pequeno", texts());
                Assert.Contains("Modelo Grande", texts());
                Assert.Contains("Recomendado para este aparelho", texts());
                Assert.Contains(texts(), t => t.StartsWith("Ocupa 3,0 GB", StringComparison.Ordinal));

                // the download shows its progress, and once it is there the model is the Assistente's
                ClickButtonWithText(panel, "Baixar (3,0 GB)");
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(panel.GetLogicalDescendants().OfType<ProgressBar>(), b => b.Value > 0);
                runtime.Gate.SetResult();
                for (int i = 0; i < 100 && !runtime.IsInstalled(FakeLocalAi.Small); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                for (int i = 0; i < 100 && !panel.Config.Providers.Any(p => p.Preset == "device"); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var provider = Assert.Single(panel.Config.Providers, p => p.Preset == "device");
                Assert.Equal(provider.Id, panel.Config.ProviderId);
                Assert.Equal("pequena", panel.Config.Model);

                // it answers: the request goes to the device, not to the internet
                panel.Send("Oi");
                for (int i = 0; i < 200 && runtime.Asked.Count == 0; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Single(runtime.Asked);
                var names = runtime.Asked[0].Tools!.Select(t => t.Name).ToList();
                Assert.Contains("read_note", names);
                Assert.DoesNotContain("manage_plugins", names);
                Assert.True(names.Count <= 12, "a IA no aparelho recebe só o subconjunto pequeno de ferramentas");
                window.Close();
                return true;
            }
            finally
            {
                LocalAi.Text = previous;
                UrbeApp.AssistantFolder = before;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
