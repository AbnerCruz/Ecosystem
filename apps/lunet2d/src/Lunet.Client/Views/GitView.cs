using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Git;

namespace Lunet.Client.Views;

/// <summary>
/// Versões (Git) of the game in the shell (P6), in plain words: what changed since the last version, save a version
/// with a sentence, and the list of versions with what each one changed. Built on Lunet.Git (no external git). Sending
/// to and receiving from a remote keeps its screen (tokens live in the device vault) and opens from here.
/// </summary>
public sealed class GitView : UserControl
{
    private const string DefaultIgnore = ".lunet/\nbin/\nobj/\n*.lunet-tmp\n";
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly string _directory;
    private readonly HashSet<string> _open = new(StringComparer.Ordinal);
    private string? _message;

    public GitView(LunetShell shell, Action close)
    {
        _shell = shell;
        _close = close;
        _directory = shell.Workspace.Project!.Directory;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Close());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        var title = LunetUi.Title("Versões");
        title.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(title);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = new Border { Child = _page, MaxWidth = 760, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xl) } });
        Content = root;
        Refresh();
    }

    public bool IsRepository => GitRepository.IsRepository(_directory);

    public bool Close() { _close(); return true; }

    /// <summary>Starts the history (git init with the game's ignore list).</summary>
    public void Start()
    {
        GitRepository.Init(_directory);
        var ignore = Path.Combine(_directory, ".gitignore");
        if (!File.Exists(ignore)) File.WriteAllText(ignore, DefaultIgnore);
        Refresh();
    }

    /// <summary>Saves a version of everything that changed (stage all + commit). Returns the new version's id.</summary>
    public ObjectId? SaveVersion(string message, string name, string email)
    {
        _shell.SaveAll(); // what is open is part of the version
        var repo = GitRepository.Open(_directory);
        var config = repo.Config;
        config.Set("user", null, "name", name.Trim());
        config.Set("user", null, "email", email.Trim());
        config.Save();
        repo.StageAll();
        var id = repo.Commit(message.Trim(), new GitSignature(name.Trim(), email.Trim(), DateTimeOffset.Now));
        _message = null;
        Refresh();
        return id;
    }

    public void Refresh()
    {
        _page.Children.Clear();
        if (!IsRepository)
        {
            _page.Children.Add(LunetUi.EmptyState(LunetIcons.Folder, "Este jogo ainda não guarda versões",
                "Comece o histórico para salvar versões do jogo e voltar a elas quando quiser. Fica tudo dentro da pasta do jogo (Git).",
                Named(LunetUi.Button("Começar o histórico", ButtonKind.Primary, LunetIcons.Plus, () => Run(Start)), "StartHistory")));
            return;
        }
        GitRepository repo;
        IReadOnlyList<StatusEntry> status;
        IReadOnlyList<CommitInfo> log;
        try
        {
            _shell.SaveAll();
            repo = GitRepository.Open(_directory);
            status = repo.GetStatus().Where(s => s.Staged != FileChange.None || s.Unstaged != FileChange.None).ToArray();
            log = repo.Head is null ? [] : repo.Log(50);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            _page.Children.Add(Notice("Não foi possível ler o histórico: " + e.Message, LunetTokens.Color.Danger));
            return;
        }
        if (_message is { } note) _page.Children.Add(Notice(note, LunetTokens.Color.Success));
        _page.Children.Add(LunetUi.Text(repo.CurrentBranch is { } branch ? "Linha: " + branch : "", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));

        // ---- changes and saving a version ----
        _page.Children.Add(Section(status.Count == 0 ? "Nada mudou desde a última versão" : $"Mudanças ({status.Count})"));
        foreach (var entry in status)
        {
            var change = entry.Unstaged != FileChange.None ? entry.Unstaged : entry.Staged;
            var path = entry.Path;
            _page.Children.Add(Expandable("change:" + path, $"{Badge(change)}  {path}",
                () => SafeDiff(() => repo.DiffWorking(path) is { Length: > 0 } d ? d : repo.DiffStaged(path))));
        }
        if (status.Count > 0)
        {
            var name = repo.Config.Get("user", null, "name") ?? "";
            var email = repo.Config.Get("user", null, "email") ?? "";
            var message = Box("O que mudou nesta versão?", "", "VersionMessage");
            _page.Children.Add(message);
            TextBox? nameBox = null, emailBox = null;
            if (name.Length == 0 || email.Length == 0)
            {
                _page.Children.Add(LunetUi.Text("Quem assina as versões (fica neste jogo):", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
                nameBox = Box("Seu nome", name, "AuthorName");
                emailBox = Box("Seu e-mail", email, "AuthorEmail");
                _page.Children.Add(nameBox);
                _page.Children.Add(emailBox);
            }
            var save = LunetUi.Button("Salvar versão", ButtonKind.Primary, LunetIcons.Check, () => Run(() =>
            {
                var text = message.Text ?? "";
                var who = nameBox?.Text ?? name;
                var mail = emailBox?.Text ?? email;
                if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Escreva em uma frase o que mudou.");
                if (string.IsNullOrWhiteSpace(who) || string.IsNullOrWhiteSpace(mail)) throw new ArgumentException("Informe nome e e-mail para assinar a versão.");
                SaveVersion(text, who, mail);
                _message = "Versão salva.";
                Refresh();
            }));
            save.Name = "SaveVersion";
            save.HorizontalAlignment = HorizontalAlignment.Stretch;
            save.MinHeight = 52;
            _page.Children.Add(save);
        }

        // ---- history ----
        _page.Children.Add(Section(log.Count == 0 ? "Nenhuma versão salva ainda" : "Versões"));
        foreach (var commit in log)
        {
            var id = commit.Id;
            _page.Children.Add(Expandable("commit:" + id, $"{commit.Summary}\n{commit.Author.Name} · {commit.Author.When.ToLocalTime():dd/MM/yyyy HH:mm}",
                () => SafeDiff(() => string.Join("\n", repo.CommitChanges(id).Select(f => $"{Badge(f.Change)}  {f.Path}")))));
        }

        if (_shell.OpenRoute is { } route)
        {
            _page.Children.Add(Section("Remoto"));
            var remote = LunetUi.Button("Enviar e receber (GitHub e outros)", ButtonKind.Secondary, LunetIcons.Share, () => route("git", _shell.State.Project));
            remote.HorizontalAlignment = HorizontalAlignment.Left;
            _page.Children.Add(remote);
        }
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            _message = null;
            Refresh();
            _page.Children.Insert(0, Notice(e.Message, LunetTokens.Color.Danger));
        }
    }

    private static string SafeDiff(Func<string> diff)
    {
        try { return diff(); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException) { return e.Message; }
    }

    private static string Badge(FileChange change) => change switch
    {
        FileChange.Added or FileChange.Untracked => "novo", FileChange.Deleted => "apagado", FileChange.Modified => "mudou", _ => "",
    };

    private Control Expandable(string key, string title, Func<string> details)
    {
        var open = _open.Contains(key);
        var header = new Button
        {
            Content = LunetUi.Text(title, LunetTokens.Type.Callout), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.S), Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
        };
        AutomationProperties.SetName(header, title.Replace('\n', ' '));
        header.Click += (_, _) => { if (!_open.Remove(key)) _open.Add(key); Refresh(); };
        if (!open) return header;
        var body = new SelectableTextBlock
        {
            Text = details(), FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label, TextWrapping = TextWrapping.NoWrap,
            Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary),
        };
        return new StackPanel
        {
            Children =
            {
                header,
                new Border
                {
                    Child = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, MaxHeight = 320 },
                    Background = LunetTokens.Brush(LunetTokens.Color.Stage), Padding = new Thickness(LunetTokens.Space.S), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
                },
            },
        };
    }

    private static TextBox Box(string hint, string text, string name)
    {
        var box = new TextBox { PlaceholderText = hint, Text = text, MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.UiFont, VerticalContentAlignment = VerticalAlignment.Center, Name = name };
        AutomationProperties.SetName(box, hint);
        return box;
    }

    private static TextBlock Section(string text)
    {
        var t = LunetUi.Text(text.ToUpperInvariant(), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
        t.Margin = new Thickness(0, LunetTokens.Space.M, 0, 0);
        return t;
    }

    private static Control Notice(string text, string color) => new Border
    {
        Child = LunetUi.Text(text, LunetTokens.Type.Label), Padding = new Thickness(LunetTokens.Space.S),
        Background = LunetTokens.Brush(LunetTokens.Color.Surface), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
        BorderBrush = LunetTokens.Brush(color), BorderThickness = new Thickness(3, 0, 0, 0),
    };

    private static Button Named(Button b, string name) { b.Name = name; return b; }
}
