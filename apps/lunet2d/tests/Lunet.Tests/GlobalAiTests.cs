using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>LUNET-521 in the shell: the AI without a game and the AI settings (no route to the previous screens).</summary>
public sealed class GlobalAiTests
{
    private sealed class Vault : Lunet.Ai.ISecretVault
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Get(string name) => _values.GetValueOrDefault(name);
        public void Set(string name, string value) => _values[name] = value;
        public void Delete(string name) => _values.Remove(name);
    }

    private sealed class Reply(string text) : HttpMessageHandler
    {
        public string? LastBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"" + text + "\"},\"finish_reason\":\"stop\"}]}",
                    System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public void TheAiWithoutAGame_IsConfiguredInTheShell_AnswersAndKeepsItsHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-global-ai-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var aiDir = Path.Combine(root, ".ai");
                var vault = new Vault();
                var host = new AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "settings.json"), vault), aiDir);
                var shell = new LunetShell(root) { AiHost = host };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();

                // Ajustes → Configurar IA opens the shell's own page (no route).
                var settings = shell.Home.ShowSettings();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var link = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "AiSettings");
                link.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var ai = Assert.IsType<GlobalAiView>(shell.Home.Sheet);
                Assert.True(ai.ShowingSettings);

                TextBox Box(string name) => window.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == name);
                window.GetVisualDescendants().OfType<ComboBox>().First(c => c.Name == "AiProvider").SelectedIndex =
                    Lunet.Ai.LunetAiProviders.All.ToList().FindIndex(p => p.Id == "custom");
                Box("AiEndpoint").Text = "https://exemplo.invalid/v1/chat/completions";
                Box("AiModel").Text = "modelo-teste";
                Box("AiKey").Text = "sk-teste-1234567890";
                window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "AiSave").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(host.Store.Load().IsConfigured);
                Assert.Equal("sk-teste-1234567890", vault.Get(Lunet.Ai.LunetAiSettingsStore.SecretFor("custom"))); // in the vault
                Assert.DoesNotContain("sk-teste", File.ReadAllText(Path.Combine(aiDir, "settings.json")));   // never in the file
                Assert.False(ai.ShowingSettings);                                                              // back to the conversation

                // A conversation with a stub provider; Back closes it; a new view reads the same history.
                var reply = new Reply("Use um List<T>.");
                var view = new GlobalAiView(shell, host, () => { }, new HttpClient(reply));
                view.Input.Text = "como guardo vários inimigos?";
                view.SendAsync().GetAwaiter().GetResult();
                Assert.Contains("inimigos", reply.LastBody);
                Assert.Equal(["user", "assistant"], view.Chat!.History.Select(t => t.Role));
                Assert.Equal("Use um List<T>.", view.Chat.History[1].Text);
                Assert.Equal(2, new GlobalAiView(shell, host, () => { }).Chat!.History.Count);
                Assert.True(shell.Back());                                                                     // closes the AI page
                Assert.Null(shell.Home.Sheet);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>LUNET-536 P8: Conexões in the shell (Registry of the Host, text.inspect locally, optional Ecosystem).</summary>
public sealed class ConnectionsTests
{
    [Fact]
    public void Connections_ListTheRegistry_InspectLocally_AndOfferTheOptionalEcosystem()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-connections-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var tests = new List<string>();
                var shell = new LunetShell(root)
                {
                    EcosystemConnection = new EcosystemConnection((text, _) => { tests.Add(text); return Task.FromResult((false, "Abra o app do Ecosystem.")); }, () => true),
                };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.WorkspaceView.Code.Surface.Text = "var a = 1;\nvar b = 2;";
                var view = shell.WorkspaceView.OpenConnections();
                Assert.Contains(view.Items, c => c.Capability == Lunet.Core.Capabilities.LunetTextInspectionCapability.CapabilityId && c.Available);
                view.InspectAsync().GetAwaiter().GetResult();
                Assert.Contains("2 linhas", view.Result);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var test = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "EcosystemTest");
                test.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(["var a = 1;\nvar b = 2;"], tests);
                Assert.Contains("Ecosystem", view.Result);
                Assert.True(shell.Back());                       // closes Conexões
                Assert.Null(shell.WorkspaceView.Studio);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>H4: streaming updates only the live block, never the whole conversation.</summary>
public sealed class AiStreamingRenderTests
{
    private sealed class Blocking : AgentRuntime.IModelProvider
    {
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ProviderId => "chat-completions";
        public async ValueTask<AgentRuntime.ModelResponse> CompleteAsync(AgentRuntime.ModelRequest request, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new([new AgentRuntime.TextBlock("pronto")], AgentRuntime.StopReason.EndTurn, new AgentRuntime.Usage(1, 1, AgentRuntime.Money.Zero("USD")));
        }
    }

    private static void WaitForModel(Blocking model)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!model.Entered.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Assert.True(model.Entered.Task.IsCompleted);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private sealed class NoVault : Lunet.Ai.ISecretVault
    {
        public string? Get(string name) => null;
        public void Set(string name, string value) { }
        public void Delete(string name) { }
    }

    [Fact]
    public void LiveUpdatesDuringARun_DoNotRebuildTheConversation()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-live-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var model = new Blocking();
                var aiDir = Path.Combine(root, ".ai");
                var shell = new LunetShell(root) { AiHost = new AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "s.json"), new NoVault()), aiDir, model) };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var ai = shell.WorkspaceView.Support.Ai;
                ai.Input.Text = "explique o Update";
                var run = ai.SendAsync();
                WaitForModel(model);
                Assert.True(ai.Running);
                var before = ai.FullRenders;
                for (var i = 0; i < 200; i++) ai.RenderLive();          // what 200 streamed chunks do
                Assert.Equal(before, ai.FullRenders);
                model.Release.SetResult();
                var until = DateTime.UtcNow.AddSeconds(20);
                while (!run.IsCompleted && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
                Assert.True(run.IsCompleted);
                Assert.False(ai.Running);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ARunKeepsItsGame_AndNeverReloadsAnotherGameWithUnsavedText()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-cross-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var model = new Blocking();
                var aiDir = Path.Combine(root, ".ai");
                var shell = new LunetShell(root) { AiHost = new AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "s.json"), new NoVault()), aiDir, model) };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.Workspace.Create("Outro");
                shell.CreateProject();
                var first = shell.Workspace.Project!.Manifest.GameId;
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var ai = shell.WorkspaceView.Support.Ai;
                ai.Input.Text = "explique";
                var run = ai.SendAsync();
                WaitForModel(model);

                shell.OpenProject("Outro");                                   // another game during the run
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(ai.Running);
                Assert.Equal(first, ai.SessionGameId);                     // still the first game's session
                shell.WorkspaceView.Code.Surface.Text += "\n// escrito agora, ainda não salvo";
                var typed = shell.WorkspaceView.Code.Surface.Text;

                model.Release.SetResult();
                var until = DateTime.UtcNow.AddSeconds(20);
                while (!run.IsCompleted && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
                Assert.Equal(typed, shell.WorkspaceView.Code.Surface.Text);   // not reloaded from disk
                Assert.NotEqual(first, ai.SessionGameId);                   // now the open game's conversation
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ClosingThePanel_DoesNotCancelTheRun_OnlyStopDoes()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-close-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var model = new Blocking();
                var aiDir = Path.Combine(root, ".ai");
                var shell = new LunetShell(root) { AiHost = new AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "s.json"), new NoVault()), aiDir, model) };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var ai = shell.WorkspaceView.Support.Ai;
                ai.Input.Text = "explique";
                var run = ai.SendAsync();
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.None);       // the panel closes…
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(ai.Running);                                           // …the run goes on
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai);         // reopening shows it
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(ai.Running);
                ai.Stop();                                                         // only Stop cancels
                var until = DateTime.UtcNow.AddSeconds(20);
                while (!run.IsCompleted && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
                Assert.True(run.IsCompleted);
                Assert.False(ai.Running);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
