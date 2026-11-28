using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Lunet.Client;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// The entry of Lunet (ADR 0013 / ADD-0036, one app): the Avalonia shell. It is an <see cref="AvaloniaActivity"/>,
/// the base Avalonia documents for embedding into an existing Android app, so Avalonia starts only when this screen
/// opens — never in the <c>:gameplay</c> and <c>:preview</c> processes. It works on the same projects as the screens
/// still served by <see cref="MainActivity"/>, which it opens by route for what has not moved yet.
/// </summary>
[Activity(
    Label = "Lunet",
    Theme = "@style/LunetTheme",
    Icon = "@mipmap/icon",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public sealed class ShellActivity : AvaloniaActivity
{
    private LunetShell? _shell;

    /// <summary>The shell's Avalonia app (the shared Lunet look) for this Android app.</summary>
    private sealed class AndroidLunetApp : LunetApp;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // AvaloniaActivity.OnCreate requires a running Avalonia app, even when this screen only forwards a request.
        if (Avalonia.Application.Current is null)
            AppBuilder.Configure<AndroidLunetApp>().UseAndroid().SetupWithoutStarting();
#if LUNET_UI_DEMO
        // tools/ci-ui-screens.sh captures the screens still served by MainActivity: forward its demo requests there.
        if (Intent?.GetStringExtra("lunet.demo") is { Length: > 0 })
        {
            base.OnCreate(savedInstanceState);
            var demo = new Intent(this, typeof(MainActivity));
            demo.PutExtras(Intent!);
            StartActivity(demo);
            Finish();
            return;
        }
#endif
        base.OnCreate(savedInstanceState);
        CreateShell();
        // In an embedded AvaloniaActivity the system Back arrives here, not on the shell's TopLevel: route it, so
        // Back closes the keyboard, then the open panel or sheet, then the page, and only then leaves the app.
        BackRequested += (_, e) => { if (_shell?.Back() == true) e.Handled = true; };
        ApplyDevice(Resources?.Configuration);
        // UX P7 (LUNET-537 D7): keys and confirmations are felt, with the system's own haptic constants.
        Lunet.Client.Ui.Haptics.KeyTap = () => Window?.DecorView?.PerformHapticFeedback(FeedbackConstants.KeyboardTap);
        Lunet.Client.Ui.Haptics.Confirm = () => Window?.DecorView?.PerformHapticFeedback(
            OperatingSystem.IsAndroidVersionAtLeast(30) ? FeedbackConstants.Confirm : FeedbackConstants.ContextClick);
        _ = UpgradeLegacyFolder();
    }

    private async Task UpgradeLegacyFolder()
    {
        var files = FilesDir!.AbsolutePath;
        var settingsStore = new SettingsStore(Path.Combine(files, "settings.json"));
        var settings = settingsStore.Load();
        if (settings.DirectProjectTree is not null || !settings.UseSharedProjectFolder ||
            settings.ProjectTree is not { } key || _shell is not { } shell) return;
        var sync = new ProjectFolderSync(files, CacheDir!.AbsolutePath);
        if (settings.ProjectWorkingCopy is not { } copy || !Directory.Exists(sync.WorkingCopy(copy))) return;
        using var dialog = new AlertDialog.Builder(this)!
            .SetTitle("Migrando os projetos")!
            .SetMessage("Conferindo os arquivos na pasta escolhida…")!
            .SetCancelable(false)!.Show();
        try
        {
            var next = await Task.Run(() => sync.ConnectDirect(settings, key, OpenDirect(this, key),
                shell.Workspace.Store.Root, legacy: null));
            if (IsFinishing || IsDestroyed) return;
            settingsStore.Save(next);
            ReopenOverNewRoot();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException or ArgumentException)
        {
            if (!IsFinishing && !IsDestroyed)
                global::Android.Widget.Toast.MakeText(this, "Pasta anterior preservada. Reconecte em Ajustes. " + ex.Message,
                    global::Android.Widget.ToastLength.Long)?.Show();
        }
        finally { if (!IsDestroyed) dialog?.Dismiss(); }
    }

    private string? _projectsRoot;

    /// <summary>Builds the shell over the current projects folder (again when Ajustes → Pasta de projetos changed it).</summary>
    private void CreateShell()
    {
        // M7: the AI uses the same settings file, Keystore vault and conversations as the previous screens: one key for all of Lunet.
        var ai = Path.Combine(FilesDir!.AbsolutePath, "ai");
        var settings = LoadEditorSettings(this);
        var files = FilesDir!.AbsolutePath;
        _projectsRoot = settings.DirectProjectTree ?? ProjectsRoot(this);
        ProjectStore store;
        string? folderError = null;
        try { store = ProjectStoreFor(this, settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            store = new ProjectStore(Path.Combine(files, "Projects"));
            folderError = "Pasta escolhida indisponível. Reconecte em Ajustes para acessar os jogos dela. " + ex.Message;
        }
        _shell = new LunetShell(store, Path.Combine(files, "Projects"))
        {
            OpenRoute = OpenRoute,
            // P3: the same declared progress as the previous Aprender screen (MainActivity.Learn).
            LearningProgressPath = Path.Combine(FilesDir!.AbsolutePath, "learning", "progress.json"),
            LauncherPreferencesPath = Path.Combine(FilesDir!.AbsolutePath, "launcher-preferences.json"), // same file as the previous Home
            // A6: the same Biblioteca as the previous screen (MainActivity.AssetLibrary).
            LibraryPath = Path.Combine(FilesDir!.AbsolutePath, "Library"),
            GitAccountPath = Path.Combine(FilesDir!.AbsolutePath, "git-account.json"),
            StudioWorkspacePath = Path.Combine(FilesDir!.AbsolutePath, "StudioWorkspace"),
            // P4: the game runs full screen in the :gameplay process, prepared by the existing pipeline.
            PlayGame = game => OpenRoute("play-game", game),
            RecoveryPath = Path.Combine(files, "editor-drafts"),
            // A4: the sound Studio plays its WAV through MediaPlayer (a temp file in the cache).
            PlaySound = wav => _ = PlayWav(wav),
            StopSound = StopWav,
            IsSoundPlaying = () => _player is not null || _wavWrite is not null,
            // P7: the same settings.json as the previous screens.
            SettingsPath = Path.Combine(FilesDir!.AbsolutePath, "settings.json"),
            // P5: Gerar APK packs with the same runtime template and per-game Keystore key as the previous screen.
            PackageGameApk = PackageGameApk,
            KeepScreenOn = on => { if (on) Window?.AddFlags(WindowManagerFlags.KeepScreenOn); else Window?.ClearFlags(WindowManagerFlags.KeepScreenOn); },
            // P8: the paired IPC client of the previous screen, now offered by the shell's Conexões (optional, NN-003).
            EcosystemConnection = new Lunet.Client.Views.EcosystemConnection(
                async (text, token) => { var r = await (_ecosystem ??= new EcosystemHostClient(this)).ConnectAndInspectAsync(text, token); return (r.Success, r.Message); },
                () => (_ecosystem ??= new EcosystemHostClient(this)).ResetTrust()),
            ProjectFolder = new Lunet.Client.Views.ProjectFolderHost(
                new ProjectFolderSync(FilesDir!.AbsolutePath, CacheDir!.AbsolutePath),
                PickProjectFolder,
                key => new AndroidProjectFolder(ContentResolver!, global::Android.Net.Uri.Parse(key)!),
                key => ReleaseProjectFolder(key),
                () => Window?.DecorView?.Post(ReopenOverNewRoot),
                key => OpenDirect(this, key)),
            AiHost = new Lunet.Client.Views.AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(ai, "settings.json"),
                new AndroidKeystoreVault(Path.Combine(NoBackupFilesDir!.AbsolutePath, "secure"))), ai,
                Knowledge: () => AndroidAiKnowledge.Load(Assets!), Web: new Lunet.Ai.LunetAiWebSession(),
                KeepAlive: HoldForAi),
        };
        // §8 of the visual language: the system's "remove animations" turns the entries into an instant appearance.
        try { Lunet.Client.Ui.LunetUi.ReduceMotion = global::Android.Provider.Settings.Global.GetFloat(ContentResolver, global::Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f; }
        catch (Java.Lang.Exception) { }
        _textScale = new Lunet.Client.TextScaleHost(_shell) { Requested = Resources?.Configuration?.FontScale ?? 1f };
        Content = _textScale;
        if (folderError is not null)
        {
            _shell.Report(folderError);
            global::Android.Widget.Toast.MakeText(this, folderError, global::Android.Widget.ToastLength.Long)?.Show();
        }
        _shell.Game.GpuFactory = (compiled, project, _) =>
        {
            try
            {
                return new IsolatedGpuPreview(this, compiled, project, _shell.Settings.HighRefreshRate);
            }
            catch (Java.Lang.Exception ex)
            {
                throw new InvalidOperationException("O Android não conseguiu iniciar a superfície do preview: " + ex.Message, ex);
            }
        };
        // Shows and hides the system keyboard (Avalonia exposes no call for it). Showing is an explicit request, as a
        // tap on Android's own EditText makes it: Avalonia only asks with SHOW_IMPLICIT, which Android ignores after
        // the person closed the keyboard with Back — the "teclado que não abre" of the editor and the AI (estudo do
        // editor, 26/11/2026). Posted, so it runs after Avalonia handed the field to the keyboard (RestartInput).
        _shell.SystemKeyboard = show =>
        {
            var view = Window?.DecorView?.FindFocus() ?? Window?.DecorView;
            if (view is null || GetSystemService(InputMethodService) is not global::Android.Views.InputMethods.InputMethodManager ime) return;
            if (show) view.Post(() => ime.ShowSoftInput(view, 0));
            else ime.HideSoftInputFromWindow(view.WindowToken, global::Android.Views.InputMethods.HideSoftInputFlags.None);
        };
    }

    // ---------------- Pasta de projetos (LUNET-536 P1: the shell's page over the system folder picker) ----------------

    private EcosystemHostClient? _ecosystem;
    private const int ProjectFolderRequest = 4139;
    private TaskCompletionSource<(string, IProjectFolderTree)?>? _folderPick;

    private Task<(string Key, IProjectFolderTree Tree)?> PickProjectFolder()
    {
        _folderPick?.TrySetResult(null);
        var pick = _folderPick = new TaskCompletionSource<(string, IProjectFolderTree)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var intent = new Intent(Intent.ActionOpenDocumentTree);
#if LUNET_EMULATOR
        if (_safJourneyPicking)
            intent.PutExtra(global::Android.Provider.DocumentsContract.ExtraInitialUri,
                global::Android.Provider.DocumentsContract.BuildDocumentUri("com.android.externalstorage.documents", "primary:Documents/LunetCi"));
#endif
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission |
            ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantPrefixUriPermission);
        try { StartActivityForResult(intent, ProjectFolderRequest); }
        catch (ActivityNotFoundException) { _folderPick = null; _shell?.Report("O seletor de pastas está indisponível."); return Task.FromResult<(string, IProjectFolderTree)?>(null); }
        return pick.Task!;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode == IsolatedGpuPreview.RequestCode)
        {
            if (_shell?.Game.Gpu is IsolatedGpuPreview preview)
            {
#if LUNET_EMULATOR
                var full = _shell.WorkspaceView.PreviewFull;
#endif
                var failure = preview.Collect(resultCode, data, _shell.Game.Write);
                if (failure is not null) _shell.Game.ReportFault(failure);
                _shell.ClosePreview();
                if (failure is not null) _shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Problems);
#if LUNET_EMULATOR
                TracePreviewReturn(full, failure);
#endif
            }
            return;
        }
        if (requestCode != ProjectFolderRequest) { base.OnActivityResult(requestCode, resultCode, data); return; }
        var pick = _folderPick;
        _folderPick = null;
        if (pick is null) return;
        if (resultCode != Result.Ok || data?.Data is not global::Android.Net.Uri tree) { pick.TrySetResult(null); return; }
        try
        {
            ContentResolver!.TakePersistableUriPermission(tree, data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission));
            pick.TrySetResult((tree.ToString()!, new AndroidProjectFolder(ContentResolver!, tree)));
        }
        catch (Java.Lang.SecurityException ex) { _shell?.Report("A pasta não autorizou escrita: " + ex.Message); pick.TrySetResult(null); }
    }

    private void ReleaseProjectFolder(string key)
    {
        try
        {
            using var tree = global::Android.Net.Uri.Parse(key)!;
            ContentResolver!.ReleasePersistableUriPermission(tree, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        }
        catch (Java.Lang.SecurityException) { }
    }

    /// <summary>The projects folder changed: the shell reopens over it (what OnResume does after the previous screen).</summary>
    private void ReopenOverNewRoot()
    {
        if (_shell is null) return;
        _shell.SaveAll();
        _shell.Shutdown();
        CreateShell();
        ApplyDevice(Resources?.Configuration);
    }

    private global::Android.Media.MediaPlayer? _player;
    private CancellationTokenSource? _wavWrite;
    private string? _wavFile;
    private bool _paused;

    /// <summary>The previous screen's packing (MainActivity.GameBuild): runtime template from the app's assets, the
    /// game's own signing key from the Android Keystore, so a new APK installs over the previous one.</summary>
    private byte[] PackageGameApk(LunetProject project, IReadOnlyDictionary<string, byte[]> files)
    {
        if (!Guid.TryParse(project.Manifest.GameId, out var gameId)) throw new InvalidDataException("GameId inválido em lunet.json.");
        using var template = Assets!.Open("templates/game-runtime.apk");
        using var buffer = new MemoryStream();
        template.CopyTo(buffer);
        if (buffer.Length < 1024) throw new InvalidDataException("Runtime Android do Lunet ausente no pacote.");
        try
        {
            return OfflineGameApkBuilder.Build(buffer.ToArray(), files, project.Manifest.PackageId, project.Name,
                new AndroidGameApkSigner(gameId), project.Manifest.Orientation);
        }
        catch (Java.Lang.Exception e) { throw new InvalidOperationException("Assinatura no aparelho falhou: " + e.Message, e); }
    }

    private async Task PlayWav(byte[] wav)
    {
        if (_paused) return;
        StopWav();
        var write = new CancellationTokenSource();
        _wavWrite = write;
        var file = Path.Combine(CacheDir!.AbsolutePath, "lunet-preview-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            await File.WriteAllBytesAsync(file, wav, write.Token);
            if (write.IsCancellationRequested) return;
            _wavFile = file;
            var player = new global::Android.Media.MediaPlayer();
            _player = player;
            player.Prepared += (_, _) =>
            {
                if (!ReferenceEquals(_player, player)) return;
                try { player.Start(); }
                catch (Java.Lang.Exception e) { WavFailed(e.Message ?? "Falha ao iniciar o áudio."); }
            };
            player.Completion += (_, _) => { if (ReferenceEquals(_player, player)) StopWav(); };
            player.Error += (_, e) =>
            {
                e.Handled = true;
                if (ReferenceEquals(_player, player)) WavFailed($"{e.What}/{e.Extra}");
            };
            player.SetDataSource(file);
            player.PrepareAsync();
        }
        catch (System.OperationCanceledException) when (write.IsCancellationRequested) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Java.Lang.Exception)
        {
            if (!write.IsCancellationRequested) WavFailed(e.Message);
        }
        finally
        {
            if (ReferenceEquals(_wavWrite, write)) _wavWrite = null;
            if (_wavFile != file) DeleteWav(file);
            write.Dispose();
        }
    }

    private void WavFailed(string message)
    {
        StopWav();
        global::Android.Util.Log.Warn("Lunet.Sound", message);
        global::Android.Widget.Toast.MakeText(this, "Não foi possível tocar o áudio.", global::Android.Widget.ToastLength.Short)?.Show();
    }

    private void StopWav()
    {
        _wavWrite?.Cancel();
        _wavWrite = null;
        var player = _player;
        _player = null;
        if (player is not null)
        {
            try { player.Release(); }
            catch (Java.Lang.Exception e) { global::Android.Util.Log.Warn("Lunet.Sound", e.Message); }
            finally { player.Dispose(); }
        }

        if (_wavFile is { } file)
        {
            _wavFile = null;
            DeleteWav(file);
        }
    }

    private static void DeleteWav(string file)
    {
        try { File.Delete(file); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            global::Android.Util.Log.Warn("Lunet.Sound", e.Message);
        }
    }

    /// <summary>
    /// The projects folder the current Lunet uses (MainActivity.ProjectFolder): the shared working copy when the
    /// person chose one and it exists, else the app's private <c>Projects</c>. Same games in both screens.
    /// </summary>
    internal static string ProjectsRoot(global::Android.Content.Context context)
    {
        var files = context.FilesDir!.AbsolutePath;
        try
        {
            var settings = new SettingsStore(Path.Combine(files, "settings.json")).Load();
            if (settings.UseSharedProjectFolder && settings.ProjectWorkingCopy is { } copy
                && Directory.Exists(Path.Combine(files, "ProjectWorkingCopies", copy)))
                return Path.Combine(files, "ProjectWorkingCopies", copy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
        return Path.Combine(files, "Projects");
    }

    private static EditorSettings LoadEditorSettings(global::Android.Content.Context context) =>
        new SettingsStore(Path.Combine(context.FilesDir!.AbsolutePath, "settings.json")).Load();

    internal static ProjectStore ProjectStoreFor(global::Android.Content.Context context, EditorSettings settings)
    {
        if (settings.DirectProjectTree is not { } key) return new ProjectStore(ProjectsRoot(context));
        var backend = OpenDirect(context, key);
        _ = backend.List(""); // permission and provider must still be available
        return new ProjectStore(backend);
    }

    private static IProjectFiles OpenDirect(global::Android.Content.Context context, string key)
    {
        var resolver = context.ContentResolver!;
        var uri = global::Android.Net.Uri.Parse(key) ?? throw new IOException("Pasta inválida.");
        new AndroidProjectFolder(resolver, uri).CheckPermission();
        return new AndroidSafProjectFiles(resolver, uri);
    }

    private bool _returningFromRoute;

    /// <summary>Opens a screen still served by MainActivity, after saving what the shell has open.</summary>
    private void OpenRoute(string route, string? project)
    {
        if (_shell is not null && !_shell.TrySaveAll()) return;
        _shell?.StopGame();
        StartActivity(MainActivity.RouteIntent(this, route, project));
        _returningFromRoute = true;
    }

    private Lunet.Client.TextScaleHost? _textScale;
    private bool _askedNotifications;

    /// <summary>
    /// An AI run keeps the process in the foreground with a notification (H4/M7). Android 13+ asks once for the
    /// permission to show it; refused, the run still works while the app is open.
    /// </summary>
    private IDisposable? HoldForAi(string game)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && !_askedNotifications &&
            CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != global::Android.Content.PM.Permission.Granted)
        {
            _askedNotifications = true;
            RequestPermissions([global::Android.Manifest.Permission.PostNotifications], 4140);
        }
        return LunetAiForegroundService.Hold(this, game, () => !_paused);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _paused = false;
        // Ajustes → Pasta de projetos may have switched the folder: the shell reopens over the new one.
        if (_shell is not null && (LoadEditorSettings(this).DirectProjectTree ?? ProjectsRoot(this)) != _projectsRoot)
        {
            _shell.SaveAll();
            _shell.Shutdown();
            CreateShell();
            ApplyDevice(Resources?.Configuration);
            return;
        }
        if (_returningFromRoute)
        {
            _returningFromRoute = false;
            _shell?.ReturnedFromRoute();
        }

    }

    protected override void OnDestroy()
    {
        _shell?.Shutdown();
        StopWav();
        _ecosystem?.Dispose();
        _ecosystem = null;
        base.OnDestroy();
    }

    protected override void OnPause()
    {
        if (_shell?.Game.State == Lunet.Client.Shell.GameState.Compiling) _shell.StopGame();
        _shell?.Game.Gpu?.SetAppVisible(false);
        _paused = true;
        if (_shell is { } shell) shell.CancelStudioAudio();
        else StopWav();
        _shell?.SaveAll(); // Android may kill the process after this
        base.OnPause();
    }

    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (_textScale is not null) _textScale.Requested = newConfig.FontScale; // UX12: the system font size
        ApplyDevice(newConfig);
    }

    /// <summary>A physical keyboard has its own symbols: the code bar steps aside.</summary>
    private void ApplyDevice(global::Android.Content.Res.Configuration? config)
    {
        if (_shell is null || config is null) return;
        _shell.HardwareKeyboard = config.Keyboard == global::Android.Content.Res.KeyboardType.Qwerty
            && config.HardKeyboardHidden == global::Android.Content.Res.HardKeyboardHidden.No;
    }

#if LUNET_EMULATOR
    private bool _safJourneyPicking;
    private bool _previewJourney;
    private string? _journeyFile;
    private string? _journeyHealthyCode;

    protected override void OnPostResume()
    {
        base.OnPostResume();
        if (_shell is not { } shell) return;
        if (Intent?.GetBooleanExtra("lunet.saf", false) == true)
        {
            Intent.RemoveExtra("lunet.saf");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = RunSafJourney(shell));
            return;
        }
        if (Intent?.GetBooleanExtra("lunet.saf.verify", false) == true)
        {
            Intent.RemoveExtra("lunet.saf.verify");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = VerifySafJourney(shell));
            return;
        }
        if (Intent?.GetBooleanExtra("lunet.e2e", false) != true) return;
        Intent.RemoveExtra("lunet.e2e");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                shell.CreateProject();
                _previewJourney = true;
                var code = shell.WorkspaceView.Code;
                _journeyFile = code.OpenPath;
                _journeyHealthyCode = code.Surface.Text;
                code.Surface.Text = """
                    using Lunet;
                    using Lunet.Graphics;

                    public sealed class PreviewG6 : Game
                    {
                        private bool rendered;

                        protected override void Update(GameTime time)
                        {
                            if (!rendered) return;
                            Log.Info("LUNET_G6_UPDATE_BLOQUEADO");
                            while (true) { }
                        }

                        protected override void Draw(GameTime time)
                        {
                            GraphicsDevice.Clear(Color.CornflowerBlue);
                            rendered = true;
                        }
                    }
                    """;
                shell.HardwareKeyboard = false;
                shell.WorkspaceView.Code.Surface.Focus();
                shell.WorkspaceView.Code.ShowKeyboard();
                global::Android.Util.Log.Info("lunet-e2e", $"editor=pronto idePid={Process.MyPid()}");
            }
            catch (Exception ex) { global::Android.Util.Log.Error("lunet-e2e", "falhou: " + ex); }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private async Task RunSafJourney(LunetShell shell)
    {
        try
        {
            shell.CreateProject("ProjetoSaf", ProjectTemplate.Blank, "portrait");
            var folder = new Lunet.Client.Views.ProjectFolderView(shell, shell.ProjectFolder!);
            _safJourneyPicking = true;
            try { await folder.Connect(); }
            finally { _safJourneyPicking = false; }
            for (var i = 0; i < 40 && ReferenceEquals(shell, _shell); i++) await Task.Delay(250);
            var directShell = _shell ?? throw new IOException("Casca indisponível.");
            if (directShell.Workspace.Store.Root is not AndroidSafProjectFiles)
                throw new IOException("O jogo não migrou à pasta escolhida: " + folder.Status);
            directShell.OpenProject("ProjetoSaf");
            var editor = directShell.WorkspaceView.Code;
            var source = editor.Surface.Text;
            const string mark = "// lunet-e2e salvamento direto";
            editor.Surface.Text += "\n" + mark;
            for (var i = 0; i < 40 && editor.IsDirty; i++) await Task.Delay(250);
            var project = directShell.Workspace.Project!;
            var path = project.Manifest.EntryPoint;
            var persisted = project.ReadText(path);
            if (persisted != source + "\n" + mark || editor.IsDirty)
                throw new IOException("O salvamento no SAF não foi confirmado.");
            new AutosaveJournal(project, directShell.RecoveryPath).WriteBuffer(path, persisted + "\n// lunet-e2e rascunho", persisted);
            global::Android.Util.Log.Info("lunet-e2e", "saf=gravado rascunho=preparado");
        }
        catch (Exception ex) { global::Android.Util.Log.Error("lunet-e2e", "falhou: " + ex); }
    }

    private async Task VerifySafJourney(LunetShell shell)
    {
        try
        {
            if (shell.Workspace.Store.Root is not AndroidSafProjectFiles)
                throw new IOException("A permissão SAF não persistiu após reiniciar.");
            shell.OpenProject("ProjetoSaf");
            var editor = shell.WorkspaceView.Code;
            if (!editor.Surface.Text.EndsWith("// lunet-e2e rascunho", StringComparison.Ordinal))
                throw new IOException("O texto do rascunho não voltou ao editor.");
            for (var i = 0; i < 40 && editor.IsDirty; i++) await Task.Delay(250);
            if (editor.IsDirty || !shell.Workspace.Project!.ReadText(editor.OpenPath!).EndsWith("// lunet-e2e rascunho", StringComparison.Ordinal))
                throw new IOException("O rascunho recuperado não foi salvo.");
            global::Android.Util.Log.Info("lunet-e2e", "saf=permissao-persistida rascunho=recuperado");
        }
        catch (Exception ex) { global::Android.Util.Log.Error("lunet-e2e", "falhou: " + ex); }
    }

    private void TracePreviewReturn(bool full, string? failure)
    {
        if (!_previewJourney || _shell is not { } shell) return;
        if (!full || shell.State.Tab != Lunet.Client.Shell.WorkspaceTab.Code
            || shell.State.Level != Lunet.Client.Shell.ShellLevel.Workspace
            || shell.WorkspaceView.Code.OpenPath != _journeyFile)
        {
            global::Android.Util.Log.Error("lunet-e2e", "falhou: o preview não retornou ao arquivo aberto.");
            return;
        }
        if (failure is not null)
        {
            if (_journeyHealthyCode is not null)
            {
                shell.WorkspaceView.Code.Surface.Text = _journeyHealthyCode;
                _journeyHealthyCode = null;
            }
            global::Android.Util.Log.Info("lunet-e2e", $"preview=recuperado idePid={Process.MyPid()} motivo={failure}");
            return;
        }

        global::Android.Util.Log.Info("lunet-e2e", $"preview=cheio retorno=editor idePid={Process.MyPid()}");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var editor = shell.WorkspaceView.Code;
            editor.Surface.Focus();
            editor.ShowKeyboard();
            editor.Surface.Caret = 0;
            editor.Keyboard?.Shortcut("{");
            global::Android.Util.Log.Info("lunet-e2e", $"barra={(editor.KeyboardVisible ? "visivel" : "oculta")} ime={(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(editor.Surface.InputTarget) ? "on" : "off")} texto={(editor.Surface.Text).Split('\n')[0]}");
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
#endif
}
