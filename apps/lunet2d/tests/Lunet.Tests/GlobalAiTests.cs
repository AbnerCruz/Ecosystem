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
