using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>H4/M7: the provider's model catalog in the shell's AI settings — real list, filter, pages, cache, manual id, honest offline.</summary>
public sealed class ModelCatalogViewTests
{
    private sealed class Vault : Lunet.Ai.ISecretVault
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Get(string name) => _values.GetValueOrDefault(name);
        public void Set(string name, string value) => _values[name] = value;
        public void Delete(string name) => _values.Remove(name);
    }

    private sealed class Models(int count, bool offline = false) : HttpMessageHandler
    {
        public int Calls;
        public string? Auth;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Auth = request.Headers.Authorization?.Parameter;
            if (offline) throw new HttpRequestException("network down", new IOException("unreachable"));
            var data = string.Join(",", Enumerable.Range(0, count).Select(i => $"{{\"id\":\"modelo-{i:00}\",\"context_length\":128000}}"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[" + data + "]}", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (Window Window, AiSettingsView View) Open(string root, HttpMessageHandler handler, string? key)
    {
        var vault = new Vault();
        var store = new Lunet.Ai.LunetAiSettingsStore(Path.Combine(root, "s.json"), vault);
        store.Save(store.Load().Settings with { ProviderId = "custom", Endpoint = "https://exemplo.invalid/v1/chat/completions", Model = "manual-1" }, key);
        var host = new AiHost(store, root, null, new Lunet.Ai.LunetAiModelCatalog(new HttpClient(handler)));
        var view = new AiSettingsView(host);
        var window = new Window { Width = 412, Height = 915, Content = view };
        window.Show();
        return (window, view);
    }

    private static T Named<T>(Window w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    private static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
    private static void Pump() { for (var i = 0; i < 20; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static List<string> Listed(Window w) =>
        Named<StackPanel>(w, "AiModelList").Children.OfType<Button>().Where(b => b.Name != "AiModelMore").Select(b => LabelOf(b)).ToList();
    private static string LabelOf(Button b) => b.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? "";

    [Fact]
    public void TheCatalog_ListsPagesFiltersAndPicks_AndIsCached()
    {
        var root = Directory.CreateTempSubdirectory("lunet-catalog-").FullName;
        UiSession.Run(() =>
        {
            var handler = new Models(45);
            var (w, _) = Open(root, handler, "sk-guardada-123456");
            Click(Named<Button>(w, "AiModelCatalog"));
            Assert.Equal("sk-guardada-123456", handler.Auth);                     // the key from the vault, not typed again
            Assert.Equal(AiSettingsView.CatalogPage, Listed(w).Count);            // first page only
            Click(Named<Button>(w, "AiModelMore"));
            Assert.Equal(45, Listed(w).Count);
            Named<TextBox>(w, "AiModelSearch").Text = "modelo-4"; Pump();
            Assert.Equal(5, Listed(w).Count);                                     // modelo-40..44
            Click(Named<StackPanel>(w, "AiModelList").Children.OfType<Button>().First(b => LabelOf(b).StartsWith("modelo-42")));
            Assert.Equal("modelo-42", Named<TextBox>(w, "AiModel").Text);
            Click(Named<Button>(w, "AiModelCatalog"));
            Assert.Equal(1, handler.Calls);                                       // cached: no second request
            Named<TextBox>(w, "AiModel").Text = "id-digitado-a-mao";              // manual id still allowed
            Assert.Equal("id-digitado-a-mao", Named<TextBox>(w, "AiModel").Text);
            w.Close();
        });
        Directory.Delete(root, true);
    }

    [Fact]
    public void Offline_OrWithoutAKey_SaysSo_AndInventsNoList()
    {
        var root = Directory.CreateTempSubdirectory("lunet-catalog-").FullName;
        UiSession.Run(() =>
        {
            var (w, _) = Open(root, new Models(5, offline: true), "sk-guardada-123456");
            Click(Named<Button>(w, "AiModelCatalog"));
            Assert.Contains("Sem conexão", Named<TextBlock>(w, "AiModelNote").Text);
            Assert.Empty(Listed(w));
            Assert.Equal("manual-1", Named<TextBox>(w, "AiModel").Text);          // the chosen model is untouched
            w.Close();

            var none = new Models(5);
            var (w2, _) = Open(Path.Combine(root, "b"), none, null);
            Click(Named<Button>(w2, "AiModelCatalog"));
            Assert.Contains("chave", Named<TextBlock>(w2, "AiModelNote").Text);
            Assert.Equal(0, none.Calls);                                          // no request without a key
            w2.Close();
        });
        Directory.Delete(root, true);
    }
}
