using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lunet.Client.Ui;
using Lunet.Core.Capabilities;

namespace Lunet.Client.Views;

/// <summary>
/// The local Ecosystem connection the head offers (Android: the paired IPC client). Optional: the game works the
/// same without it (NN-003). <paramref name="Test"/> connects and inspects a text; <paramref name="Forget"/> resets the
/// pairing (false while a session is open).
/// </summary>
public sealed record EcosystemConnection(Func<string, CancellationToken, Task<(bool Success, string Message)>> Test, Func<bool> Forget);

/// <summary>
/// Conexões of the open game, in the shell (LUNET-536 P8): the capabilities the Host's Registry has in this game's
/// Context (the Registry is the only source; this page keeps no list of its own), text.inspect run locally over the
/// open code, and — when the head has one — the optional paired connection to the Ecosystem.
/// </summary>
public sealed class ConnectionsView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M) };
    private readonly TextBlock _result = LunetUi.Text("", LunetTokens.Type.BodySmall, LunetTokens.Color.TextSecondary);
    private readonly CancellationTokenSource _lifetime = new();

    public ConnectionsView(LunetShell shell, Action close)
    {
        _shell = shell;
        _close = close;
        _result.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Close());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        var title = LunetUi.Title("Conexões");
        title.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(title);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = _page });
        Content = root;
        Build();
    }

    /// <summary>The last result shown (tests).</summary>
    public string Result => _result.Text ?? "";
    public IReadOnlyList<LunetConnectionDescriptor> Items { get; private set; } = [];

    public bool Close() { _lifetime.Cancel(); _close(); return true; }

    private void Build()
    {
        _page.Children.Clear();
        if (_shell.Workspace.Project is not { } project) { _page.Children.Add(LunetUi.Secondary("Abra um jogo para ver as conexões dele.")); return; }
        using (var session = LunetCapabilityHost.CreateDefault().OpenForProject(project))
        {
            _page.Children.Add(LunetUi.Text("Contexto: " + string.Join(" → ", session.Context.Path.Select(s => $"{s.Level}:{s.Id}")), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
            Items = session.Connections();
            if (Items.Count == 0) _page.Children.Add(LunetUi.Secondary("Nenhuma capability registrada neste Host."));
            foreach (var c in Items)
            {
                var card = new StackPanel { Spacing = 2 };
                card.Children.Add(LunetUi.Text($"{c.Capability} {c.Version} · {c.Provider}", LunetTokens.Type.Callout, c.Available ? LunetTokens.Color.TextPrimary : LunetTokens.Color.TextSecondary));
                card.Children.Add(LunetUi.Text(c.Available ? "Disponível neste jogo" : c.ContextMatches ? "Falta permissão: " + string.Join(", ", c.MissingPermissions) : "Fora deste contexto",
                    LunetTokens.Type.Caption, c.Available ? LunetTokens.Color.Success : LunetTokens.Color.Warning));
                _page.Children.Add(LunetUi.Card(card));
            }
            _page.Children.Add(LunetUi.Text("Fonte: Registry do Host. Conexões não mantém cadastro próprio.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        }
        var inspect = LunetUi.Button("Inspecionar o código aberto (local)", ButtonKind.Secondary, LunetIcons.Code, () => _ = InspectAsync());
        inspect.Name = "InspectText";
        _page.Children.Add(inspect);
        if (_shell.EcosystemConnection is { } ecosystem)
        {
            _page.Children.Add(LunetUi.Divider());
            _page.Children.Add(LunetUi.Text("Ecosystem neste aparelho", LunetTokens.Type.Callout));
            _page.Children.Add(LunetUi.Secondary("Opcional: o Lunet funciona igual sem ele. No primeiro uso, compare o código de 6 dígitos e aprove no app do Ecosystem."));
            var test = LunetUi.Button("Conectar e testar", ButtonKind.Primary, null, async () =>
            {
                _result.Text = "Conectando…";
                var text = _shell.WorkspaceView.Code.Surface.Text;
                var (ok, message) = await ecosystem.Test(text.Length > 0 ? text : "Olá do Lunet pelo IPC local.", _lifetime.Token);
                _result.Text = (ok ? "Conexão autenticada. " : "") + message;
            });
            test.Name = "EcosystemTest";
            _page.Children.Add(test);
            var forget = LunetUi.Button("Esquecer conexão", ButtonKind.Ghost, null, () =>
                _result.Text = ecosystem.Forget() ? "Confiança local apagada. O próximo uso pede novo pareamento." : "Há uma sessão aberta. Encerre-a antes de esquecer a conexão.");
            forget.Name = "EcosystemForget";
            _page.Children.Add(forget);
        }
        _page.Children.Add(_result);
    }

    /// <summary>text.inspect over the selection (or the whole open code), run by the local Host (tests await it).</summary>
    public async Task InspectAsync()
    {
        if (_shell.Workspace.Project is not { } project) return;
        var surface = _shell.WorkspaceView.Code.Surface;
        var doc = surface.Text;
        var selected = surface.SelectionEnd > surface.SelectionStart;
        var text = selected ? doc[surface.SelectionStart..surface.SelectionEnd] : doc;
        using var session = LunetCapabilityHost.CreateDefault().OpenForProject(project);
        var capability = session.Discover().FirstOrDefault(c => c.Capability == LunetTextInspectionCapability.CapabilityId);
        if (capability is null) { _result.Text = "text.inspect não está disponível neste contexto."; return; }
        var result = await session.InvokeAsync(capability.Capability, new Version(1, 0, 0),
            System.Text.Json.JsonSerializer.SerializeToElement(new { text }), _lifetime.Token);
        if (!result.Succeeded || result.Output is not { } output) { _result.Text = "text.inspect falhou: " + (result.Error ?? "resposta inválida"); return; }
        _result.Text = $"{(selected ? "Seleção" : "Código aberto")}: {output.GetProperty("characters").GetInt32()} caracteres, " +
            $"{output.GetProperty("words").GetInt32()} palavras, {output.GetProperty("lines").GetInt32()} linhas. Execução local, sem provider externo.";
    }
}
