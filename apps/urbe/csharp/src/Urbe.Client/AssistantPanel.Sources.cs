using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Urbe.Core.Assistant;

namespace Urbe.Client;

public sealed partial class AssistantPanel
{
    private string? SourceUrl(string id)
    {
        var source = _conv.WebSources.FirstOrDefault(s => s.Id == id);
        if (source is null) return null;
        try { return AssistantWebSession.ValidateUrl(source.FinalUrl).AbsoluteUri; }
        catch (AiToolException) { return null; }
    }

    private void ShowSources()
    {
        var body = OpenView("Fontes da pesquisa");
        body.Children.Add(Hint("Encontrar um link não confirma seu conteúdo. As fontes preservam seus identificadores para conferir as citações da conversa."));
        var search = new TextBox { PlaceholderText = "Buscar título, domínio ou identificador" };
        var filter = new ComboBox { ItemsSource = new[] { "Todas", "Páginas consultadas", "Somente busca", "Falhas de leitura" }, SelectedIndex = 0 };
        var list = new StackPanel { Spacing = 8 };
        body.Children.Add(search);
        body.Children.Add(filter);
        body.Children.Add(list);
        int limit = 20;
        var failed = _conv.Messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_use" && b.Name == "web_fetch" &&
            ResultFor(b.Id) is { IsError: true }).ToList();
        void Paint()
        {
            list.Children.Clear();
            var query = (search.Text ?? "").Trim();
            var hits = _conv.WebSources.Where(s => filter.SelectedIndex switch
                { 1 => s.Status == "página acessada", 2 => s.Status != "página acessada", 3 => false, _ => true })
                .Where(s => (s.Id + " " + s.Title + " " + s.Domain).Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.Status == "página acessada").ToList();
            if (filter.SelectedIndex != 3) list.Children.Add(Hint(hits.Count + " fontes correspondentes"));
            foreach (var source in hits.Take(limit))
            {
                var row = new StackPanel { Spacing = 4 };
                var link = LinkButton(source.Id, () => ShowSource(source));
                link.Content = Wrap(source.Id + " · " + source.Title, 13, UrbeTheme.Accent);
                link.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                row.Children.Add(link);
                row.Children.Add(Wrap(source.Domain + " · " + (source.Status == "página acessada" ? "Página consultada (trechos)" : "Somente resultado de busca"), 12, UrbeTheme.Text2));
                list.Children.Add(Card(row));
            }
            if (hits.Count > limit) list.Children.Add(LinkButton("Mostrar mais 20", () => { limit += 20; Paint(); }));
            if (filter.SelectedIndex is 0 or 3)
                foreach (var call in failed.Where(c => ((string?)c.Input?["url"] ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)).Take(limit))
                {
                    var row = new StackPanel { Spacing = 4 };
                    row.Children.Add(Wrap((string?)call.Input?["url"] ?? "Fonte não identificada", 12, UrbeTheme.Text));
                    row.Children.Add(Wrap("Leitura falhou · " + ResultFor(call.Id)!.Content, 12, UrbeTheme.Danger));
                    list.Children.Add(Card(row));
                }
        }
        search.TextChanged += (_, _) => { limit = 20; Paint(); };
        filter.SelectionChanged += (_, _) => { limit = 20; Paint(); };
        Paint();
    }

    private void ShowSource(AiWebSource source)
    {
        var body = OpenView(source.Id + " · Fonte", ShowSources);
        body.Children.Add(Wrap(source.Title, 18, UrbeTheme.Text));
        body.Children.Add(Wrap(source.Status == "página acessada" ? "Página consultada: trechos disponíveis ao agente." : "Somente busca: o conteúdo original não foi confirmado.", 13, UrbeTheme.Text2));
        body.Children.Add(Wrap("Publicado: " + (source.Published ?? "não informado") + "\nAcesso: " + source.Accessed, 12, UrbeTheme.Text2));
        body.Children.Add(Pre(source.FinalUrl, false));
        if (SourceUrl(source.Id) is { } url)
            body.Children.Add(LinkButton("Abrir fonte original", () => _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(url))));
        var evidence = _conv.Messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_result" &&
            b.Content.StartsWith("[" + source.Id + "] ", StringComparison.Ordinal)).ToList();
        foreach (var result in evidence)
            body.Children.Add(Lazy(Label("Trecho devolvido ao agente", 12, UrbeTheme.Text2), () => Pre(result.Content, result.IsError)));
    }
}
