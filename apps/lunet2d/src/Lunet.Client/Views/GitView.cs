using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;
using Lunet.Git;

namespace Lunet.Client.Views;

/// <summary>
/// Versões (Git) of the game in the shell (P6), in plain words: what changed since the last version, save a version
/// with a sentence, and the list of versions with what each one changed. Built on Lunet.Git (no external git). Sending
/// to and receiving from a remote (GitHub and others) is here too: the address, the account (token in the app's private
/// storage) and Receber/Enviar.
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

        RemoteSection(repo);
    }

    // ---------- Sending and receiving (remote) ----------

    private bool _accountOpen;
    private string? _remoteStatus;
    private bool _remoteBusy;

    public string? RemoteUrl => GitRepository.IsRepository(_directory) ? GitRepository.Open(_directory).Remotes().FirstOrDefault(r => r.Name == "origin")?.Url : null;
    public GitAccount Account => new GitAccountStore(_shell.GitAccountPath).Load();
    public string? RemoteStatus => _remoteStatus;

    public void SetRemoteUrl(string url)
    {
        url = url.Trim();
        if (url.Length == 0) throw new ArgumentException("Cole o endereço do repositório (https://…).");
        GitRepository.Open(_directory).SetRemote("origin", url);
        _remoteStatus = "Endereço salvo.";
        Refresh();
    }

    public void OpenAccount() { _accountOpen = true; Refresh(); }

    public void SaveAccount(GitAccount account)
    {
        new GitAccountStore(_shell.GitAccountPath).Save(account);
        _accountOpen = false;
        _remoteStatus = "Conta salva.";
        Refresh();
    }

    /// <summary>Receives the server's versions and merges them into this line (pull).</summary>
    public Task<string> ReceiveAsync() => Remote("Recebendo…", async (repo, transport, progress) =>
    {
        var account = Account;
        var name = account.Name.Length > 0 ? account.Name : repo.Config.Get("user", null, "name") ?? "";
        var email = account.Email.Length > 0 ? account.Email : repo.Config.Get("user", null, "email") ?? "";
        if (name.Length == 0 || email.Length == 0) throw new ArgumentException("Informe nome e e-mail na conta para receber versões.");
        var outcome = await repo.PullAsync("origin", transport, new GitSignature(name, email, DateTimeOffset.Now), progress);
        _shell.WorkspaceView.ReloadFromDisk();
        return outcome switch { MergeOutcome.UpToDate => "Já estava atualizado.", MergeOutcome.FastForward => "Recebido: o jogo está igual ao do servidor.", _ => "Recebido e juntado com as suas versões." };
    });

    /// <summary>Sends this line's versions to the server (push).</summary>
    public Task<string> SendAsync() => Remote("Enviando…", async (repo, transport, progress) =>
    {
        var branch = repo.CurrentBranch ?? throw new ArgumentException("Volte para uma linha antes de enviar.");
        if (repo.Head is null) throw new ArgumentException("Salve uma versão antes de enviar.");
        var result = await repo.PushAsync("origin", branch, transport, progress: progress);
        return result.UpToDate ? "O servidor já tinha estas versões." : $"Enviado para {branch}.";
    });

    private async Task<string> Remote(string busy, Func<GitRepository, IGitTransport, IProgress<string>, Task<string>> operation)
    {
        if (_remoteBusy) return _remoteStatus ?? "";
        var url = RemoteUrl;
        if (url is null) { _remoteStatus = "Informe o endereço do repositório primeiro."; Refresh(); return _remoteStatus; }
        _remoteBusy = true;
        _remoteStatus = busy;
        Refresh();
        try
        {
            _shell.SaveAll();
            var repo = GitRepository.Open(_directory);
            var progress = new Progress<string>(p => { if (_remoteBusy) { _remoteStatus = p; Refresh(); } });
            _remoteStatus = await operation(repo, _shell.GitTransport(url, Account), progress);
        }
        catch (Exception e) when (e is GitException or IOException or HttpRequestException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or TaskCanceledException)
        {
            _remoteStatus = "Não foi possível: " + e.Message;
        }
        finally { _remoteBusy = false; }
        Refresh();
        return _remoteStatus!;
    }

    private void RemoteSection(GitRepository repo)
    {
        _page.Children.Add(Section("Enviar e receber (GitHub e outros)"));
        var url = repo.Remotes().FirstOrDefault(r => r.Name == "origin")?.Url ?? "";
        var account = Account;
        if (_remoteStatus is { } status)
            _page.Children.Add(Notice(status, status.StartsWith("Não foi possível", StringComparison.Ordinal) ? LunetTokens.Color.Danger : LunetTokens.Color.Success));
        var address = Box("https://github.com/voce/jogo.git", url, "RemoteUrl");
        Avalonia.Input.TextInput.TextInputOptions.SetContentType(address, Avalonia.Input.TextInput.TextInputContentType.Url);
        var saveUrl = Named(LunetUi.Button("Salvar", ButtonKind.Secondary, null, () => Run(() => SetRemoteUrl(address.Text ?? ""))), "SaveRemote");
        var urlRow = new DockPanel();
        DockPanel.SetDock(saveUrl, Dock.Right);
        saveUrl.Margin = new Thickness(LunetTokens.Space.Xs, 0, 0, 0);
        urlRow.Children.Add(saveUrl);
        urlRow.Children.Add(address);
        _page.Children.Add(urlRow);
        if (url.Length > 0)
        {
            var actions = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };
            var receive = Named(LunetUi.Button("Receber", ButtonKind.Secondary, LunetIcons.Download, () => _ = ReceiveAsync()), "Receive");
            var send = Named(LunetUi.Button("Enviar", ButtonKind.Primary, LunetIcons.Share, () => _ = SendAsync()), "Send");
            foreach (var b in new[] { receive, send }) { b.MinHeight = 52; b.HorizontalAlignment = HorizontalAlignment.Stretch; b.Margin = new Thickness(LunetTokens.Space.Xxs); b.Opacity = _remoteBusy ? 0.5 : 1; actions.Children.Add(b); }
            _page.Children.Add(actions);
        }
        var who = account.CanCommit ? $"Conta · {account.Name} <{account.Email}>" : "Conta ainda não configurada";
        var token = account.Token.Length > 0 ? "token configurado" : "sem token: só recebe de repositórios públicos";
        var header = new Button
        {
            Content = LunetUi.Text($"{who}\n{token}", LunetTokens.Type.Callout), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.S), Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M), Name = "AccountHeader",
        };
        AutomationProperties.SetName(header, who);
        header.Click += (_, _) => { _accountOpen = !_accountOpen; Refresh(); };
        _page.Children.Add(header);
        if (!_accountOpen) return;
        var name = Box("Seu nome", account.Name, "AccountName");
        var email = Box("Seu e-mail", account.Email, "AccountEmail");
        var user = Box("Usuário do servidor (pode ficar vazio no GitHub)", account.Username, "AccountUser");
        var secret = Box("Token de acesso (fica só neste aparelho)", account.Token, "AccountToken");
        secret.PasswordChar = '•';
        var save = Named(LunetUi.Button("Salvar conta", ButtonKind.Primary, LunetIcons.Check, () => Run(() => SaveAccount(new GitAccount
        {
            Name = (name.Text ?? "").Trim(), Email = (email.Text ?? "").Trim(), Username = (user.Text ?? "").Trim(), Token = (secret.Text ?? "").Trim(),
        }))), "SaveAccount");
        save.HorizontalAlignment = HorizontalAlignment.Left;
        foreach (var c in new Control[] { name, email, user, secret, save }) _page.Children.Add(c);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is GitException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
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
