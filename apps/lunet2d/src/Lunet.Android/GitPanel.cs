using Android.App;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Lunet.Git;
using AndroidColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>Painel Git do projeto: alterações, histórico, ramos e remoto (push/pull), sobre <see cref="GitRepository"/>.</summary>
internal sealed class GitPanel
{
    private const string DefaultIgnore = ".lunet/\nbin/\nobj/\n*.lunet-tmp\n";

    private readonly Activity _activity;
    private readonly string _directory;
    private readonly GitAccountStore _accounts;
    private readonly Action _beforeOperation;
    private readonly Action _afterTreeChanged;
    private readonly Dialog _dialog;
    private readonly TextView _title;
    private readonly TextView _progress;
    private readonly LinearLayout _content;
    private readonly ScrollView _scroll;
    private readonly int _pad;
    private GitRepository? _repo;
    private string _tab = "changes";
    private bool _busy;

    private GitPanel(Activity activity, string directory, GitAccountStore accounts, Action beforeOperation, Action afterTreeChanged)
    {
        _activity = activity;
        _directory = directory;
        _accounts = accounts;
        _beforeOperation = beforeOperation;
        _afterTreeChanged = afterTreeChanged;
        _pad = (int)(10 * activity.Resources!.DisplayMetrics!.Density);

        var root = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(AndroidColor.Argb(255, 24, 26, 32));

        var bar = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
        bar.SetPadding(_pad / 2, _pad / 2, _pad / 2, _pad / 2);
        _title = new TextView(activity) { TextSize = 16 };
        _title.SetPadding(_pad, 0, _pad, 0);
        bar.AddView(_title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        bar.AddView(Button("↻", Refresh));
        bar.AddView(Button("✕", () => _dialog!.Dismiss()));
        root.AddView(bar);

        var tabs = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
        foreach (var (id, label) in new[] { ("changes", "Alterações"), ("history", "Histórico"), ("branches", "Ramos"), ("remote", "Remoto") })
        {
            var captured = id;
            var tab = Button(label, () => { _tab = captured; Render(); });
            tab.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
            tab.SetPadding(2, 0, 2, 0);
            tabs.AddView(tab);
        }
        root.AddView(tabs);

        _progress = new TextView(activity) { TextSize = 11 };
        _progress.SetPadding(_pad, 2, _pad, 2);
        root.AddView(_progress);

        _scroll = new ScrollView(activity);
        _content = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        _content.SetPadding(_pad, _pad / 2, _pad, _pad * 2);
        _scroll.AddView(_content);
        root.AddView(_scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _dialog = new Dialog(activity, global::Android.Resource.Style.ThemeBlackNoTitleBarFullScreen);
        _dialog.SetContentView(root);
    }

    public static void Show(Activity activity, string projectDirectory, GitAccountStore accounts, Action beforeOperation, Action afterTreeChanged)
    {
        var panel = new GitPanel(activity, projectDirectory, accounts, beforeOperation, afterTreeChanged);
        panel._dialog.Show();
        panel.Refresh();
    }

    // ---------- Infraestrutura ----------

    private Button Button(string label, Action action)
    {
        var button = new Button(_activity) { Text = label };
        button.SetAllCaps(false);
        button.Click += (_, _) => action();
        return button;
    }

    private TextView Text(string text, int size = 13, bool bold = false, int? color = null)
    {
        var view = new TextView(_activity) { Text = text, TextSize = size };
        if (bold) view.SetTypeface(null, TypefaceStyle.Bold);
        if (color is { } c) view.SetTextColor(new AndroidColor(c));
        view.SetPadding(0, _pad / 3, 0, _pad / 3);
        return view;
    }

    private void Refresh()
    {
        _repo = GitRepository.IsRepository(_directory) ? GitRepository.Open(_directory) : null;
        Render();
    }

    private void Render()
    {
        _content.RemoveAllViews();
        _scroll.ScrollTo(0, 0);
        if (_repo is null)
        {
            _title.Text = "Git";
            _content.AddView(Text("Este projeto ainda não usa Git.\n\nO Git guarda o histórico do seu jogo, permite voltar no tempo, criar ramos de experimento e sincronizar com o GitHub."));
            _content.AddView(Button("Iniciar repositório Git", InitRepository));
            return;
        }
        try
        {
            var branch = _repo.CurrentBranch;
            _title.Text = "Git · " + (branch ?? $"(commit {_repo.Head?.Short ?? "vazio"})");
            switch (_tab)
            {
                case "history": RenderHistory(); break;
                case "branches": RenderBranches(); break;
                case "remote": RenderRemote(); break;
                default: RenderChanges(); break;
            }
        }
        catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
        {
            _content.AddView(Text("Erro ao ler o repositório: " + ex.Message, color: unchecked((int)0xFFFF6B6B)));
        }
    }

    /// <summary>Roda uma operação fora da thread de UI, com progresso, e atualiza o painel no fim.</summary>
    private void Work(string label, Func<IProgress<string>, Task<string?>> work, bool treeChanged = false)
    {
        if (_busy)
        {
            Toast.MakeText(_activity, "Aguarde a operação em andamento.", ToastLength.Short)?.Show();
            return;
        }
        _busy = true;
        _beforeOperation();
        _progress.Text = label + "…";
        var progress = new Progress<string>(message => _progress.Text = message);
        Task.Run(async () =>
        {
            try { return (Message: await work(progress).ConfigureAwait(false), Error: (string?)null); }
            catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                return (null, ex.Message);
            }
        }).ContinueWith(task => _activity.RunOnUiThread(() =>
        {
            _busy = false;
            _progress.Text = "";
            if (treeChanged) _afterTreeChanged();
            Refresh();
            var (message, error) = task.Result;
            if (error is not null) new AlertDialog.Builder(_activity)!.SetTitle(label)!.SetMessage(error)!.SetPositiveButton("Ok", (_, _) => { })!.Show();
            else if (message is not null) Toast.MakeText(_activity, message, ToastLength.Long)?.Show();
        }));
    }

    private GitSignature? Signature()
    {
        var account = _accounts.Load();
        if (!account.CanCommit)
        {
            AskAccount(() => { });
            return null;
        }
        return new GitSignature(account.Name.Trim(), account.Email.Trim(), DateTimeOffset.Now);
    }

    private void ShowDiff(string title, string diff)
    {
        var text = string.IsNullOrEmpty(diff) ? "(sem diferenças)" : diff;
        var span = new SpannableStringBuilder(text);
        var position = 0;
        foreach (var line in text.Split('\n'))
        {
            AndroidColor? color = line.StartsWith("+++", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal) ? AndroidColor.Rgb(160, 165, 175)
                : line.StartsWith('+') ? AndroidColor.Rgb(120, 200, 120)
                : line.StartsWith('-') ? AndroidColor.Rgb(230, 110, 110)
                : line.StartsWith("@@", StringComparison.Ordinal) ? AndroidColor.Rgb(110, 190, 230)
                : null;
            if (color is { } c && line.Length > 0) span.SetSpan(new ForegroundColorSpan(c), position, position + line.Length, SpanTypes.ExclusiveExclusive);
            position += line.Length + 1;
        }
        var view = new TextView(_activity) { TextFormatted = span, TextSize = 11 };
        view.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        view.SetHorizontallyScrolling(true);
        view.SetPadding(_pad, _pad, _pad, _pad);
        view.SetTextIsSelectable(true);
        var vertical = new ScrollView(_activity);
        var horizontal = new HorizontalScrollView(_activity);
        horizontal.AddView(view);
        vertical.AddView(horizontal);
        new AlertDialog.Builder(_activity)!.SetTitle(title)!.SetView(vertical)!.SetPositiveButton("Fechar", (_, _) => { })!.Show();
    }

    private void Ask(string title, string hint, string initial, Action<string> accept, bool multiline = false, string ok = "Ok")
    {
        var input = new EditText(_activity) { Hint = hint, Text = initial };
        if (multiline) { input.SetLines(4); input.SetHorizontallyScrolling(false); }
        else input.SetSingleLine(true);
        var holder = new LinearLayout(_activity) { Orientation = Orientation.Vertical };
        holder.SetPadding(_pad * 2, _pad, _pad * 2, 0);
        holder.AddView(input);
        new AlertDialog.Builder(_activity)!.SetTitle(title)!.SetView(holder)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton(ok, (_, _) => accept((input.Text ?? "").Trim()))!.Show();
    }

    private void Confirm(string title, string message, Action accept, string ok = "Confirmar") =>
        new AlertDialog.Builder(_activity)!.SetTitle(title)!.SetMessage(message)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton(ok, (_, _) => accept())!.Show();

    // ---------- Iniciar ----------

    private void InitRepository() => Work("Iniciando Git", _ =>
    {
        GitRepository.Init(_directory);
        var ignore = System.IO.Path.Combine(_directory, ".gitignore");
        if (!File.Exists(ignore)) File.WriteAllText(ignore, DefaultIgnore);
        return Task.FromResult<string?>("Repositório criado. Faça o primeiro commit na aba Alterações.");
    });

    // ---------- Alterações ----------

    private static string Letter(FileChange change, bool staged) => change switch
    {
        FileChange.Added => "A",
        FileChange.Modified => "M",
        FileChange.Deleted => "D",
        FileChange.Untracked => staged ? " " : "?",
        _ => " ",
    };

    private void RenderChanges()
    {
        var repo = _repo!;
        var status = repo.GetStatus();
        var row = new LinearLayout(_activity) { Orientation = Orientation.Horizontal };
        row.AddView(Button("Preparar tudo", () => Work("Preparando", _ => { repo.StageAll(); return Task.FromResult<string?>(null); })));
        row.AddView(Button("Commit…", () => AskCommit(status)));
        _content.AddView(row);

        if (status.Count == 0)
        {
            _content.AddView(Text(repo.Head is null ? "Nada para commitar ainda. Crie ou edite arquivos." : "Sem alterações. Tudo está commitado."));
            return;
        }
        _content.AddView(Text("Legenda: 1ª letra = preparado para o commit, 2ª = ainda não preparado (A novo, M modificado, D apagado, ? sem rastreio).", 11));
        foreach (var entry in status)
        {
            var captured = entry;
            var button = Button($"{Letter(entry.Staged, true)}{Letter(entry.Unstaged, false)}  {entry.Path}", () => ShowFileMenu(captured));
            button.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
            button.TextSize = 12;
            button.Gravity = GravityFlags.Left | GravityFlags.CenterVertical;
            _content.AddView(button);
        }
    }

    private void ShowFileMenu(StatusEntry entry)
    {
        var repo = _repo!;
        var actions = new List<(string Label, Action Run)>();
        if (entry.Unstaged != FileChange.None) actions.Add(("Ver diff (não preparado)", () => ShowDiff(entry.Path, repo.DiffWorking(entry.Path))));
        if (entry.Staged != FileChange.None) actions.Add(("Ver diff (preparado)", () => ShowDiff(entry.Path + " (preparado)", repo.DiffStaged(entry.Path))));
        if (entry.Unstaged != FileChange.None) actions.Add(("Preparar", () => Work("Preparando", _ => { repo.Stage(entry.Path); return Task.FromResult<string?>(null); })));
        if (entry.Staged != FileChange.None) actions.Add(("Tirar da preparação", () => Work("Removendo da preparação", _ => { repo.Unstage(entry.Path); return Task.FromResult<string?>(null); })));
        if (entry.Unstaged != FileChange.None)
            actions.Add((entry.Unstaged == FileChange.Untracked ? "Apagar arquivo" : "Descartar alterações", () =>
                Confirm("Descartar", $"As alterações de {entry.Path} serão perdidas e não dá para desfazer.", () =>
                    Work("Descartando", _ => { repo.DiscardChanges(entry.Path); return Task.FromResult<string?>(null); }, treeChanged: true), "Descartar")));
        new AlertDialog.Builder(_activity)!.SetTitle(entry.Path)!
            .SetItems(actions.Select(a => a.Label).ToArray(), (_, args) => actions[args.Which].Run())!.Show();
    }

    private void AskCommit(IReadOnlyList<StatusEntry> status)
    {
        var signature = Signature();
        if (signature is null) return;
        var hasStaged = status.Any(s => s.Staged != FileChange.None);
        var form = new LinearLayout(_activity) { Orientation = Orientation.Vertical };
        form.SetPadding(_pad * 2, _pad, _pad * 2, 0);
        var message = new EditText(_activity) { Hint = "Mensagem do commit (o que mudou e por quê)" };
        message.SetLines(3);
        var stageAll = new CheckBox(_activity) { Text = "Preparar todas as mudanças antes de commitar", Checked = !hasStaged };
        form.AddView(message);
        form.AddView(stageAll);
        new AlertDialog.Builder(_activity)!.SetTitle("Novo commit")!.SetView(form)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Commitar", (_, _) =>
            {
                var text = (message.Text ?? "").Trim();
                var prepare = stageAll.Checked;
                Work("Commitando", _ =>
                {
                    var repo = _repo!;
                    if (prepare) repo.StageAll();
                    var id = repo.Commit(text, signature);
                    return Task.FromResult<string?>($"Commit {id.Short} criado");
                });
            })!.Show();
    }

    // ---------- Histórico ----------

    private void RenderHistory()
    {
        var repo = _repo!;
        var commits = repo.Log(80);
        if (commits.Count == 0)
        {
            _content.AddView(Text("Ainda não há commits."));
            return;
        }
        foreach (var commit in commits)
        {
            var captured = commit;
            var merge = commit.Parents.Count > 1 ? " ⑂" : "";
            var button = Button($"{commit.Id.Short}{merge}  {commit.Summary}\n{commit.Author.Name} · {commit.Author.When.LocalDateTime:dd/MM/yyyy HH:mm}", () => ShowCommit(captured));
            button.Gravity = GravityFlags.Left | GravityFlags.CenterVertical;
            button.TextSize = 12;
            _content.AddView(button);
        }
    }

    private void ShowCommit(CommitInfo commit)
    {
        var repo = _repo!;
        var actions = new List<(string Label, Action Run)>
        {
            ("Ver arquivos alterados", () => ShowCommitFiles(commit)),
            ("Reverter este commit", () => Confirm("Reverter", $"Cria um novo commit que desfaz \"{commit.Summary}\".", () =>
            {
                var signature = Signature();
                if (signature is null) return;
                Work("Revertendo", _ => Task.FromResult<string?>($"Revertido no commit {repo.Revert(commit.Id, signature).Short}"), treeChanged: true);
            }, "Reverter")),
            ("Criar ramo a partir daqui", () => Ask("Novo ramo", "nome-do-ramo", "", name =>
                Work("Criando ramo", _ =>
                {
                    repo.CreateBranch(name, commit.Id);
                    return Task.FromResult<string?>($"Ramo {name} criado");
                }))),
            ("Voltar a este commit (HEAD destacado)", () => Confirm("Trocar de commit", "O projeto volta ao estado deste commit. Para continuar trabalhando, crie um ramo aqui.", () =>
                Work("Trocando", _ =>
                {
                    repo.Checkout(commit.Id.Hex);
                    return Task.FromResult<string?>("Projeto no commit " + commit.Id.Short);
                }, treeChanged: true))),
        };
        var header = $"{commit.Id.Hex}\n{commit.Author.Name} <{commit.Author.Email}>\n{commit.Author.When.LocalDateTime:dd/MM/yyyy HH:mm}\n\n{commit.Message}";
        new AlertDialog.Builder(_activity)!.SetTitle(commit.Id.Short)!.SetMessage(header)!
            .SetNegativeButton("Fechar", (_, _) => { })!
            .SetNeutralButton("Ações…", (_, _) =>
                new AlertDialog.Builder(_activity)!.SetTitle(commit.Id.Short)!.SetItems(actions.Select(a => a.Label).ToArray(), (_, args) => actions[args.Which].Run())!.Show())!
            .Show();
    }

    private void ShowCommitFiles(CommitInfo commit)
    {
        var repo = _repo!;
        var files = repo.CommitChanges(commit.Id);
        if (files.Count == 0)
        {
            Toast.MakeText(_activity, "Sem mudanças de arquivos.", ToastLength.Short)?.Show();
            return;
        }
        var labels = files.Select(f => $"{Letter(f.Change, false)}  {f.Path}").ToArray();
        new AlertDialog.Builder(_activity)!.SetTitle($"Arquivos de {commit.Id.Short}")!
            .SetItems(labels, (_, args) => ShowDiff(files[args.Which].Path, repo.DiffCommitFile(commit.Id, files[args.Which].Path)))!.Show();
    }

    // ---------- Ramos ----------

    private void RenderBranches()
    {
        var repo = _repo!;
        _content.AddView(Button("Novo ramo…", () => Ask("Novo ramo", "nome-do-ramo", "", name =>
            Work("Criando ramo", _ =>
            {
                repo.CheckoutNewBranch(name);
                return Task.FromResult<string?>($"Ramo {name} criado e ativo");
            }), ok: "Criar")));
        var branches = repo.Branches(includeRemote: true);
        if (branches.Count == 0) _content.AddView(Text("Sem ramos ainda: faça o primeiro commit."));
        foreach (var branch in branches)
        {
            var captured = branch;
            var button = Button($"{(branch.IsCurrent ? "● " : "   ")}{branch.Name}{(branch.IsRemote ? "  (remoto)" : "")}   {branch.Id.Short}", () => ShowBranchMenu(captured));
            button.Gravity = GravityFlags.Left | GravityFlags.CenterVertical;
            _content.AddView(button);
        }
    }

    private void ShowBranchMenu(BranchInfo branch)
    {
        var repo = _repo!;
        var actions = new List<(string Label, Action Run)>();
        if (!branch.IsCurrent)
            actions.Add((branch.IsRemote ? "Criar ramo local e trocar" : "Trocar para este ramo", () =>
                Work("Trocando de ramo", _ => { repo.Checkout(branch.Name); return Task.FromResult<string?>("Ramo ativo: " + repo.CurrentBranch); }, treeChanged: true)));
        if (!branch.IsRemote && !branch.IsCurrent)
        {
            actions.Add(("Mesclar no ramo atual", () =>
            {
                var signature = Signature();
                if (signature is null) return;
                Work("Mesclando", _ =>
                {
                    var outcome = repo.Merge(branch.Id, signature);
                    return Task.FromResult<string?>(outcome switch
                    {
                        MergeOutcome.UpToDate => "Já estava atualizado",
                        MergeOutcome.FastForward => "Mesclado (avanço rápido)",
                        _ => "Mesclado com um commit de mesclagem",
                    });
                }, treeChanged: true);
            }));
            actions.Add(("Excluir ramo", () => Confirm("Excluir ramo", $"Excluir \"{branch.Name}\"?", () =>
                Work("Excluindo", _ =>
                {
                    try { repo.DeleteBranch(branch.Name); }
                    catch (GitException ex) when (ex.Message.Contains("mesclados", StringComparison.Ordinal))
                    {
                        throw new GitException(ex.Message + " Se quiser mesmo excluir, toque em Excluir de novo depois de mesclar, ou use outro cliente Git.");
                    }
                    return Task.FromResult<string?>("Ramo excluído");
                }), "Excluir")));
        }
        if (actions.Count == 0)
        {
            Toast.MakeText(_activity, "Este é o ramo atual.", ToastLength.Short)?.Show();
            return;
        }
        new AlertDialog.Builder(_activity)!.SetTitle(branch.Name)!
            .SetItems(actions.Select(a => a.Label).ToArray(), (_, args) => actions[args.Which].Run())!.Show();
    }

    // ---------- Remoto ----------

    private void RenderRemote()
    {
        var repo = _repo!;
        var account = _accounts.Load();
        var remote = repo.Remotes().FirstOrDefault(r => r.Name == "origin");
        _content.AddView(Text("Servidor (origin)", 14, bold: true));
        _content.AddView(Text(remote is null ? "Nenhum endereço configurado." : remote.Url));
        _content.AddView(Button(remote is null ? "Definir endereço…" : "Trocar endereço…", () =>
            Ask("Endereço do repositório", "https://github.com/usuario/projeto.git", remote?.Url ?? "", url =>
            {
                try { repo.SetRemote("origin", url); }
                catch (GitException ex) { Toast.MakeText(_activity, ex.Message, ToastLength.Long)?.Show(); return; }
                Render();
            })));

        _content.AddView(Text("Conta", 14, bold: true));
        _content.AddView(Text(account.CanCommit ? $"{account.Name} <{account.Email}>\nToken: {(account.Token.Length > 0 ? "configurado" : "não configurado (só leitura de repositórios públicos)")}" : "Nome e e-mail ainda não configurados."));
        _content.AddView(Button("Editar conta…", () => AskAccount(Render)));

        _content.AddView(Text("Sincronizar", 14, bold: true));
        var row = new LinearLayout(_activity) { Orientation = Orientation.Horizontal };
        row.AddView(Button("Buscar", () => RunRemote("Buscando", async (transport, progress) =>
        {
            var result = await repo.FetchAsync("origin", transport, progress).ConfigureAwait(false);
            return result.UpdatedRefs.Count == 0 ? "Nada novo no servidor" : $"Atualizado: {string.Join(", ", result.UpdatedRefs)}";
        })));
        row.AddView(Button("Pull", () =>
        {
            var signature = Signature();
            if (signature is null) return;
            RunRemote("Pull", async (transport, progress) =>
            {
                var outcome = await repo.PullAsync("origin", transport, signature, progress).ConfigureAwait(false);
                return outcome switch
                {
                    MergeOutcome.UpToDate => "Já estava atualizado",
                    MergeOutcome.FastForward => "Atualizado (avanço rápido)",
                    _ => "Mesclado com o servidor",
                };
            }, treeChanged: true);
        }));
        row.AddView(Button("Push", () =>
        {
            var branch = repo.CurrentBranch;
            if (branch is null)
            {
                Toast.MakeText(_activity, "Volte para um ramo antes de enviar.", ToastLength.Long)?.Show();
                return;
            }
            RunRemote("Enviando", async (transport, progress) =>
            {
                var result = await repo.PushAsync("origin", branch, transport, progress: progress).ConfigureAwait(false);
                return result.UpToDate ? "O servidor já está atualizado" : $"Enviado: {branch}";
            });
        }));
        _content.AddView(row);
        _content.AddView(Text("No GitHub, crie um token em Settings → Developer settings → Personal access tokens, com permissão de acesso ao repositório (\"repo\"). O token fica guardado só neste aparelho.", 11));
    }

    private void RunRemote(string label, Func<IGitTransport, IProgress<string>, Task<string?>> operation, bool treeChanged = false)
    {
        var repo = _repo!;
        var url = repo.Remotes().FirstOrDefault(r => r.Name == "origin")?.Url;
        if (string.IsNullOrEmpty(url))
        {
            Toast.MakeText(_activity, "Defina o endereço do servidor primeiro.", ToastLength.Long)?.Show();
            return;
        }
        var account = _accounts.Load();
        Work(label, progress =>
        {
            var transport = new HttpGitTransport(url, account.Username.Length > 0 ? account.Username : null, account.Token.Length > 0 ? account.Token : null);
            return operation(transport, progress);
        }, treeChanged);
    }

    private void AskAccount(Action done)
    {
        var account = _accounts.Load();
        var form = new LinearLayout(_activity) { Orientation = Orientation.Vertical };
        form.SetPadding(_pad * 2, _pad, _pad * 2, 0);
        EditText Field(string hint, string value, bool secret = false)
        {
            var field = new EditText(_activity) { Hint = hint, Text = value };
            field.SetSingleLine(true);
            if (secret) field.InputType = global::Android.Text.InputTypes.ClassText | global::Android.Text.InputTypes.TextVariationPassword;
            form.AddView(field);
            return field;
        }
        var name = Field("Seu nome (aparece nos commits)", account.Name);
        var email = Field("Seu e-mail", account.Email);
        var user = Field("Usuário do servidor (opcional)", account.Username);
        var token = Field("Token de acesso (para push/pull)", account.Token, secret: true);
        new AlertDialog.Builder(_activity)!.SetTitle("Conta Git")!.SetView(form)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) =>
            {
                var updated = new GitAccount { Name = (name.Text ?? "").Trim(), Email = (email.Text ?? "").Trim(), Username = (user.Text ?? "").Trim(), Token = (token.Text ?? "").Trim() };
                try { _accounts.Save(updated); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(_activity, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
                    return;
                }
                if (!updated.CanCommit) Toast.MakeText(_activity, "Informe um nome e um e-mail válidos para poder commitar.", ToastLength.Long)?.Show();
                done();
            })!.Show();
    }
}
