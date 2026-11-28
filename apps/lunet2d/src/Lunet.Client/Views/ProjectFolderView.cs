using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// What the head gives the shell to use a folder the person chooses (Android: the system folder picker, SAF).
/// <paramref name="Pick"/> opens the picker and returns the folder's key and access, or null when cancelled;
/// <paramref name="Open"/> reopens a saved key; <paramref name="Release"/> gives the access back;
/// <paramref name="RootChanged"/> reopens the shell over the new projects folder.
/// </summary>
public sealed record ProjectFolderHost(
    ProjectFolderSync Sync,
    Func<Task<(string Key, IProjectFolderTree Tree)?>> Pick,
    Func<string, IProjectFolderTree> Open,
    Action<string> Release,
    Action RootChanged,
    Func<string, IProjectFiles>? OpenDirect = null);

/// <summary>
/// Ajustes → Pasta de projetos, in the shell (LUNET-536 P1; the previous screen's menu, same rules and settings):
/// choose or reconnect a folder, publish a verified revision, open an older revision in another working copy,
/// switch back to the previous copy, go back to the internal games, release the access.
/// </summary>
public sealed class ProjectFolderView : UserControl
{
    private readonly LunetShell _shell;
    private readonly ProjectFolderHost _host;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.M, Margin = new Thickness(LunetTokens.Space.M) };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary);
    private bool _busy;
    private IReadOnlyList<string>? _revisions;
    private string? _confirm;

    public ProjectFolderView(LunetShell shell, ProjectFolderHost host)
    {
        _shell = shell;
        _host = host;
        Content = new ScrollViewer { Content = _page };
        Build();
    }

    public string Status => _status.Text ?? "";
    public bool Busy => _busy;

    private void Build()
    {
        _page.Children.Clear();
        var s = _shell.Settings;
        if (_host.OpenDirect is not null)
        {
            BuildDirect(s);
            return;
        }
        var active = s.UseSharedProjectFolder;
        _page.Children.Add(LunetUi.Title(active ? "Pasta escolhida" : "Armazenamento interno"));
        _page.Children.Add(LunetUi.Secondary(active
            ? "Você edita uma cópia de trabalho neste aparelho e grava revisões conferidas na pasta. Nada é apagado: os jogos internos e as cópias anteriores ficam."
            : "Seus jogos ficam na memória interna do Lunet. Escolha uma pasta sua para guardar revisões fora do app (por exemplo, uma pasta sincronizada)."));
        if (s.ProjectRevision is { } rev) _page.Children.Add(LunetUi.Text("Revisão atual: " + Short(rev), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));

        if (_confirm == "connect")
        {
            _page.Children.Add(LunetUi.Notice("Uma revisão existente na pasta abre numa cópia de trabalho. Numa pasta nova, o Lunet copia e confere os jogos atuais antes de trocar. As cópias anteriores são preservadas.", LunetTokens.Color.Info));
            Add("Escolher a pasta", ButtonKind.Primary, () => { _confirm = null; _ = Connect(); }, "FolderPick");
            Add("Cancelar", ButtonKind.Ghost, () => { _confirm = null; Build(); }, "FolderCancel");
        }
        else if (_revisions is { } list)
        {
            _page.Children.Add(LunetUi.Text("Abrir uma revisão numa nova cópia de trabalho", LunetTokens.Type.Callout));
            if (list.Count == 0) _page.Children.Add(LunetUi.Secondary("A pasta ainda não tem uma revisão."));
            for (var i = 0; i < list.Count; i++)
            {
                var revision = list[i];
                Add($"Revisão {list.Count - i} · {Short(revision)}", ButtonKind.Secondary, () => { _revisions = null; _ = Run("Carregando revisão…", st => _host.Sync.Restore(st, Tree(st), revision), rootChanges: true); }, "FolderRevision-" + i);
            }
            Add("Voltar", ButtonKind.Ghost, () => { _revisions = null; Build(); }, "FolderBack");
        }
        else
        {
            Add(s.ProjectTree is null ? "Escolher uma pasta" : "Escolher ou reconectar pasta", active ? ButtonKind.Secondary : ButtonKind.Primary,
                () => { _confirm = "connect"; Build(); }, "FolderChoose");
            if (active)
            {
                Add("Gravar revisão na pasta", ButtonKind.Primary, () => _ = Run("Gravando e conferindo a revisão…", st => _host.Sync.SaveRevision(st, Tree(st)), rootChanges: false,
                    done: "Revisão conferida na pasta escolhida."), "FolderSave");
                Add("Recuperar revisão da pasta", ButtonKind.Secondary, () => _ = ListRevisions(), "FolderRevisions");
                if (_host.Sync.CanSwap(s))
                    Add("Alternar com a cópia anterior", ButtonKind.Secondary, () => Apply(_host.Sync.Swap(_shell.Settings), rootChanges: true), "FolderSwap");
                Add("Usar os jogos internos", ButtonKind.Ghost, () => Apply(_host.Sync.SetActive(_shell.Settings, false), rootChanges: true), "FolderInternal");
                Add("Liberar acesso à pasta", ButtonKind.Ghost, Disconnect, "FolderRelease");
            }
            else if (s.ProjectTree is not null && s.ProjectWorkingCopy is not null)
                Add("Retomar a cópia de trabalho da pasta", ButtonKind.Secondary, () =>
                {
                    var next = _host.Sync.SetActive(_shell.Settings, true);
                    if (next is null) { _confirm = "connect"; Build(); } else Apply(next, rootChanges: true);
                }, "FolderResume");
        }
        _page.Children.Add(_status);
    }

    private void BuildDirect(EditorSettings settings)
    {
        var direct = settings.DirectProjectTree is not null;
        _page.Children.Add(LunetUi.Title(direct ? "Pasta escolhida" : "Armazenamento interno"));
        _page.Children.Add(LunetUi.Secondary(direct
            ? "Os jogos são abertos e gravados diretamente na pasta escolhida."
            : settings.UseSharedProjectFolder
                ? "Escolha a pasta para migrar os jogos da cópia de trabalho atual. O Lunet confere os arquivos antes de trocar."
                : "Escolha uma pasta para abrir e gravar seus jogos fora do armazenamento do app."));
        if (_confirm == "connect")
        {
            _page.Children.Add(LunetUi.Notice("Os arquivos atuais serão conferidos na pasta escolhida. Se houver duas versões do mesmo jogo, o Lunet preserva ambas e informa o conflito.", LunetTokens.Color.Info));
            Add("Escolher a pasta", ButtonKind.Primary, () => { _confirm = null; _ = Connect(); }, "FolderPick");
            Add("Cancelar", ButtonKind.Ghost, () => { _confirm = null; Build(); }, "FolderCancel");
        }
        else
        {
            Add(direct ? "Escolher ou reconectar pasta" : "Escolher uma pasta", ButtonKind.Primary,
                () => { _confirm = "connect"; Build(); }, "FolderChoose");
            if (direct)
            {
                Add("Usar os jogos internos", ButtonKind.Ghost, () => ReturnToInternal(release: false), "FolderInternal");
                Add("Liberar acesso à pasta", ButtonKind.Ghost, () => ReturnToInternal(release: true), "FolderRelease");
            }
        }
        _page.Children.Add(_status);
    }

    private void ReturnToInternal(bool release)
    {
        if (_shell.Settings.DirectProjectTree is not { } key || !_shell.TrySaveAll()) return;
        var next = _shell.Settings.Clone();
        next.DirectProjectTree = null;
        Apply(next, rootChanges: true);
        if (release && _shell.Settings.DirectProjectTree is null) _host.Release(key);
    }

    private static string Short(string revision) => revision.Length > 68 ? revision[^68..^60] : revision;

    private void Add(string label, ButtonKind kind, Action action, string name)
    {
        var b = LunetUi.Button(label, kind, null, () => { if (!_busy) action(); });
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.Name = name;
        b.IsEnabled = !_busy;
        _page.Children.Add(b);
    }

    private IProjectFolderTree Tree(EditorSettings s) =>
        _host.Open(s.ProjectTree ?? throw new IOException("Nenhuma pasta escolhida."));

    private void Apply(EditorSettings? next, bool rootChanges, string? done = null)
    {
        if (next is null) { Build(); return; }
        var before = _shell.Settings.DirectProjectTree ?? _host.Sync.ProjectsRoot(_shell.Settings);
        try { if (!_shell.TrySaveAll()) return; _shell.SaveSettings(next); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Text = "Não foi possível salvar: " + e.Message;
            return;
        }
        _status.Text = done ?? "";
        Build();
        if (rootChanges && (_host.OpenDirect is not null || (next.DirectProjectTree ?? _host.Sync.ProjectsRoot(next)) != before)) _host.RootChanged();
    }

    /// <summary>Runs a folder operation off the UI thread; any failure keeps the games and the settings as they were.</summary>
    private async Task Run(string title, Func<EditorSettings, EditorSettings> operation, bool rootChanges, string? done = null)
    {
        if (_busy) return;
        _busy = true;
        _status.Text = title;
        Build();
        var settings = _shell.Settings;
        if (!_shell.TrySaveAll()) { _busy = false; Build(); return; }
        EditorSettings? next = null;
        try { next = await Task.Run(() => operation(settings)); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or ProjectException)
        {
            _status.Text = "Pasta indisponível. Jogos preservados. " + e.Message;
        }
        _busy = false;
        if (next is not null) Apply(next, rootChanges, done);
        else Build();
    }

    /// <summary>The picker, then the connection (tests await it).</summary>
    public async Task Connect()
    {
        if (_busy) return;
        var picked = await _host.Pick();
        if (picked is not { } p) { Build(); return; }
        if (_host.OpenDirect is { } openDirect)
        {
            var directBefore = _shell.Settings.DirectProjectTree;
            var current = _shell.Workspace.Store.Root;
            await Run("Conferindo os jogos na pasta escolhida…",
                s => _host.Sync.ConnectDirect(s, p.Key, openDirect(p.Key), current, p.Tree), rootChanges: true);
            if (_shell.Settings.DirectProjectTree == p.Key)
            {
                if (directBefore is not null && directBefore != p.Key) _host.Release(directBefore);
            }
            else if (directBefore != p.Key && _shell.Settings.ProjectTree != p.Key) _host.Release(p.Key);
            return;
        }
        var previous = _shell.Settings.ProjectTree;
        await Run("Conferindo a pasta e os jogos…", s => _host.Sync.Connect(s, p.Key, p.Tree), rootChanges: true);
        if (_shell.Settings.ProjectTree == p.Key)
        {
            if (previous is not null && previous != p.Key) _host.Release(previous); // the old folder's access is no longer needed
        }
        else if (previous != p.Key) _host.Release(p.Key); // not used: give the new access back
    }

    private async Task ListRevisions()
    {
        if (_busy) return;
        _busy = true;
        _status.Text = "Consultando revisões…";
        Build();
        var s = _shell.Settings;
        try { _revisions = await Task.Run(() => Tree(s).Revisions()); _status.Text = ""; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { _status.Text = "Pasta indisponível. " + e.Message; }
        _busy = false;
        Build();
    }

    /// <summary>The previous screen kept the working copy in use after releasing the folder; so does this.</summary>
    private void Disconnect()
    {
        if (_shell.Settings.ProjectTree is not { } key) return;
        _host.Release(key);
        _status.Text = "Acesso liberado. A cópia de trabalho ainda pode ser usada neste aparelho.";
        Build();
    }
}
