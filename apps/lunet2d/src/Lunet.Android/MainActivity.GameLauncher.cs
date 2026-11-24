using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Android.Gles;
using Lunet.Compiler;
using Lunet.Core;
using Path = System.IO.Path;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private bool _gameplayPreparing;
    private string? _gameplaySession;
    private string? _gameplayId;
    private string? _gameplayFolder;

    private void ShowGameLauncher()
    {
        EnterLauncherSurface("play");
        RefreshLauncherCatalog();
        var root = Vertical();
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(8));
        root.SetBackgroundColor(LunetUi.Background);
        root.AddView(LunetUi.Label(this, "Jogar", LunetUi.TextHero, LunetUi.Text, bold: true));
        root.AddView(LunetUi.Label(this, "Seus jogos locais · sem abrir o editor ou instalar APK",
            LunetUi.TextBody, LunetUi.TextMuted));
        var scroll = new ScrollView(this) { FillViewport = true };
        var content = LunetUi.Column(this, 12);
        content.SetPadding(0, Dp(16), 0, Dp(12));
        var names = _store.List();
        if (names.Count == 0)
        {
            content.AddView(LunetUi.Label(this, "Crie um projeto para começar a jogar.", LunetUi.TextBody, LunetUi.TextMuted));
            content.AddView(LunetUi.Button(this, "Criar projeto", AskForProjectName, ButtonStyle.Primary));
        }
        for (int i = 0; i < names.Count; i += 2)
        {
            var row = LunetUi.Row(this, 8);
            for (int j = i; j < Math.Min(i + 2, names.Count); j++)
                row.AddView(GameplayCard(names[j]), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            if (i + 1 == names.Count)
                row.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1f));
            content.AddView(row, LunetUi.FillWidth());
        }
        scroll.AddView(content);
        root.AddView(scroll, Fill(1));
        SetLauncherContent(root, "play", scroll);
    }

    private View GameplayCard(string folder)
    {
        var card = LunetUi.Column(this, 5);
        card.SetPadding(Dp(10), Dp(12), Dp(10), Dp(8));
        card.Background = LunetUi.Pressable(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
        string title = folder;
        string? id = LauncherId(folder);
        string status = "Identidade ausente ou duplicada";
        LunetProject? project = null;
        try
        {
            project = _store.Open(folder);
            title = project.Name;
            if (id is not null && LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, id) == folder &&
                Guid.TryParse(id, out var gameId))
            {
                var last = Path.Combine(GameplayActivity.Data(this, gameId), "last-played");
                status = File.Exists(last) ? "Jogado " + File.GetLastWriteTime(last).ToString("dd/MM HH:mm") :
                    "Compilação ainda não verificada";
            }
            else id = null;
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            id = null;
            status = "Projeto indisponível";
        }
        var identity = id;
        card.Clickable = identity is not null;
        card.ContentDescription = "Jogar " + title + ", pasta " + folder;
        if (identity is not null) card.Click += (_, _) => PlayFromLauncher(folder);
        View? icon = project is null ? null : TryGameplayIcon(project);
        if (icon is null)
        {
            var initial = LunetUi.Label(this, title.Length == 0 ? "?" : title[..1].ToUpperInvariant(),
                26, LunetUi.AccentText, bold: true);
            initial.Gravity = GravityFlags.Center;
            initial.Background = LunetUi.Shape(this, LunetUi.AccentSoft, LunetUi.Radius);
            icon = initial;
        }
        card.AddView(icon, LunetUi.FillWidth(Dp(86)));
        var name = LunetUi.Label(this, title, LunetUi.TextBody, LunetUi.Text, bold: true);
        name.SetSingleLine(true);
        name.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        card.AddView(name);
        card.AddView(LunetUi.Label(this, folder + (identity is null ? "" : " · " + identity[..8]),
            LunetUi.TextCaption, LunetUi.TextMuted));
        card.AddView(LunetUi.Label(this, status, LunetUi.TextCaption, LunetUi.TextMuted));
        var actions = LunetUi.Row(this, 4);
        if (identity is not null)
            actions.AddView(LunetUi.Icon(this, "▶", "Jogar " + title, () => PlayFromLauncher(folder)),
                new LinearLayout.LayoutParams(0, Dp(44), 1f));
        actions.AddView(LunetUi.Icon(this, "⋮", "Editar projeto " + title, () => OpenProject(folder)),
            new LinearLayout.LayoutParams(0, Dp(44), 1f));
        card.AddView(actions);
        return card;
    }

    private View? TryGameplayIcon(LunetProject project)
    {
        // Optional project convention; existing manifest schemas do not need migration.
        try
        {
            var path = Path.Combine(project.Directory, "Content", "icon.png");
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(Path.GetDirectoryName(path)!) & FileAttributes.ReparsePoint) != 0) return null;
            using var options = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(path, options);
            if (options.OutWidth <= 0 || options.OutHeight <= 0) return null;
            var size = Math.Max(options.OutWidth, options.OutHeight);
            int sample = 1;
            while (size / sample > 256) sample *= 2;
            options.InJustDecodeBounds = false;
            options.InSampleSize = sample;
            var bitmap = BitmapFactory.DecodeFile(path, options);
            if (bitmap is null) return null;
            var image = new ImageView(this);
            image.SetImageBitmap(bitmap);
            image.SetScaleType(ImageView.ScaleType.FitCenter);
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private static string GameplayToolchain() => string.Join("|",
        new[] { typeof(Game).Assembly, typeof(GameCompiler).Assembly, typeof(MainActivity).Assembly,
            typeof(Lunet.Runtime.GameLoader).Assembly }.Select(a => a.ManifestModule.ModuleVersionId.ToString("N"))) +
        "|" + Environment.Version;

    private async Task PrepareGameplay(string folderName, string? expectedId)
    {
        if (_gameplayPreparing || _gameplaySession is not null) return;
        _gameplayPreparing = true;
        PlayableRevision? revision = null;
        bool transferred = false;
        using var cancel = new CancellationTokenSource();
        AlertDialog? dialog = null;
        try
        {
            ShowGameLauncher();
            var id = expectedId ?? LauncherId(folderName);
            var resolved = LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, id);
            if (id is null || resolved is null)
                throw new ProjectException("Este jogo não tem um GameId persistente e único. Abra o projeto para corrigir a identidade.");
            folderName = resolved;
            var project = _store.Open(folderName);
            if (new AutosaveJournal(project).FindRecoveries().Count > 0)
                throw new ProjectException("Há alterações não recuperadas. Abra o editor e decida como restaurá-las antes de jogar.");
            dialog = new AlertDialog.Builder(this)!.SetTitle("Preparando " + project.Name)!
                .SetMessage("Capturando a revisão local…")!.SetCancelable(false)!
                .SetNegativeButton("Cancelar", (_, _) => cancel.Cancel())!.Show();
            var ct = cancel.Token;
            var staging = GameplayActivity.Staging(this);
            var cache = new PlayableBuildCache(GameplayActivity.Cache(this));
            var toolchain = GameplayToolchain();
            revision = await Task.Run(() => PlayableRevision.Capture(project, staging, toolchain, ct), ct);
            ct.ThrowIfCancellationRequested();
            if (IsFinishing || IsDestroyed) return;
            var snapshot = revision;
            var assembly = await Task.Run(() => cache.TryRead(snapshot.GameId, snapshot.Key), ct);
            if (assembly is null)
            {
                dialog.SetMessage("Compilando a revisão " + snapshot.Key[..8] + "…");
                var sources = snapshot.Sources.Select(s => new SourceFile(s.Path, s.Text)).ToArray();
                var result = await Task.Run(() => SharedCompiler.Value.Compile("LunetGame" + Guid.NewGuid().ToString("N"), sources), ct);
                ct.ThrowIfCancellationRequested();
                if (!result.Success || result.Assembly is null)
                {
                    var errors = string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => $"{d.FilePath}:{d.Line} · {d.Id} {d.Message}"));
                    throw new InvalidDataException("A revisão atual não compila. Nenhuma versão anterior será aberta automaticamente.\n" + errors);
                }
                assembly = result.Assembly;
                var bytes = assembly;
                await Task.Run(() => cache.Publish(snapshot.GameId, snapshot.Key, bytes), ct);
            }
            ct.ThrowIfCancellationRequested();
            if (IsFinishing || IsDestroyed) return;
            dialog.SetMessage("Abrindo o jogo…");
            await File.WriteAllBytesAsync(Path.Combine(snapshot.Directory, "Game.dll"), assembly, ct);
            ct.ThrowIfCancellationRequested();
            if (IsFinishing || IsDestroyed) return;
            _gameplaySession = Path.GetFileName(snapshot.Directory);
            _gameplayId = snapshot.GameId.ToString("D");
            _gameplayFolder = folderName;
            using var intent = new Intent(this, typeof(GameplayActivity));
            intent.PutExtra(GameplayActivity.SessionExtra, _gameplaySession);
            StartActivityForResult(intent, GameplayActivity.RequestCode);
            transferred = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("Lunet.Gameplay", ex.ToString());
            if (!IsFinishing && !IsDestroyed) ShowGameplayError(folderName, ex.Message);
        }
        finally
        {
            dialog?.Dismiss();
            if (!transferred)
            {
                revision?.Dispose();
                _gameplaySession = _gameplayId = _gameplayFolder = null;
            }
            _gameplayPreparing = false;
        }
    }

    private void ShowGameplayError(string? folder, string message)
    {
        var dialog = new AlertDialog.Builder(this)!.SetTitle("Não foi possível jogar")!
            .SetMessage(message)!.SetNegativeButton("Fechar", (_, _) => { })!;
        if (folder is not null && _store.List().Contains(folder, StringComparer.Ordinal))
            dialog.SetPositiveButton("Editar projeto", (_, _) => OpenProject(folder));
        dialog.Show();
    }

    private void CompleteGameplay(Result result, Intent? data)
    {
        var session = _gameplaySession;
        var gameId = _gameplayId;
        var folder = _gameplayFolder;
        _gameplaySession = _gameplayId = _gameplayFolder = null;
        if (Guid.TryParseExact(session, "N", out var key))
        {
            var path = Path.Combine(GameplayActivity.Staging(this), key.ToString("N"));
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (IOException ex) { global::Android.Util.Log.Warn("Lunet.Gameplay", ex.Message); }
            catch (UnauthorizedAccessException ex) { global::Android.Util.Log.Warn("Lunet.Gameplay", ex.Message); }
        }
        ShowGameLauncher();
        folder = gameId is not null ? LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, gameId) : folder;
        if (result != Result.Ok)
            ShowGameplayError(folder, data?.GetStringExtra(GameplayActivity.ErrorExtra) ??
                "A sessão terminou inesperadamente. O projeto e os saves persistidos foram preservados.");
    }
}
