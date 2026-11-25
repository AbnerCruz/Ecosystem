using Android.Content;

namespace Lunet.Android;

/// <summary>
/// ADR 0013 (one Lunet): the Avalonia shell (<see cref="ShellActivity"/>) is the entry of the app; the screens not yet
/// moved into it open here through a route, over the same projects. Back from a routed screen saves and returns to
/// the shell, so the person never lands on this activity's own Home.
/// </summary>
public sealed partial class MainActivity
{
    /// <summary>Intent extra with the destination; <see cref="RouteProjectExtra"/> names the game when the route needs one.</summary>
    public const string RouteExtra = "lunet.route";
    public const string RouteProjectExtra = "lunet.route.project";

    /// <summary>The routes the shell can open (the screens still served by this activity).</summary>
    public static readonly IReadOnlyList<string> Routes =
        ["studios", "learn", "explore", "play", "play-game", "library", "settings", "project-folder", "ai-settings", "ai", "git", "assets", "notes", "apk", "editor"];

    private bool _fromShell;
    // A route whose screen is a dialog (Biblioteca, Gerar APK, Ajustes): nothing of the previous Home shows behind it, and
    // closing the last dialog returns to the shell.
    private bool _routeIsDialog;
    // P4: the shell's Jogar page asked to play one game: prepare it here, show it, then return to the shell.
    private bool _playFromShell;

    /// <summary>Starts this activity on a route, reusing the running instance (no stack of copies).</summary>
    public static Intent RouteIntent(global::Android.Content.Context context, string route, string? project)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.PutExtra(RouteExtra, route);
        if (project is not null) intent.PutExtra(RouteProjectExtra, project);
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        return intent;
    }

    private bool IsRouted(Intent? intent) => intent?.GetStringExtra(RouteExtra) is { Length: > 0 };

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (IsRouted(intent)) { Intent = intent; Route(intent!); }
    }

    /// <summary>Opens the screen a route names (after the activity loaded its settings and projects).</summary>
    private void Route(Intent intent)
    {
        _fromShell = true;
        var route = intent.GetStringExtra(RouteExtra);
        var project = intent.GetStringExtra(RouteProjectExtra);
        intent.RemoveExtra(RouteExtra);
        try
        {
            if (project is not null && route is "ai" or "git" or "assets" or "notes" or "apk" or "editor")
            {
                // The shell saved its documents before leaving; reload the game from disk.
                LoadProjectFolder();
                OpenProject(project);
            }
            _routeIsDialog = route is "library" or "apk" or "settings" or "project-folder" or "ai-settings";
            _playFromShell = route == "play-game";
            if (_routeIsDialog || _playFromShell)
            {
                var plain = new global::Android.Views.View(this);
                plain.SetBackgroundColor(LunetUi.Background);
                SetContentView(plain);
            }
            switch (route)
            {
                case "studios": ShowLauncherStudios(); break;
                case "learn": ShowLearning(); break;
                case "explore": OpenInspirationExplorer(); break;
                case "play": ShowGameLauncher(); break;
                case "play-game":
                    LoadProjectFolder();
                    RefreshLauncherCatalog();
                    if (project is not null) PlayFromLauncher(project); else LeaveToShell();
                    break;
                case "library": ShowReusableAssetLibrary(); break;
                case "settings": ShowSettings(); break;
                case "project-folder": LoadProjectFolder(); ShowProjectFolder(); break;
                case "ai-settings": ShowAiSettings(); break;
                case "ai": if (_project is not null) ShowAiAssistant(); else ShowWorkspaceAi(); break;
                case "git": if (_project is not null) ShowGit(); break;
                case "assets": if (_project is not null) ShowAssets(); break;
                case "notes": if (_project is not null) ShowNotes(); break;
                case "apk": if (_project is not null) AskBuildGameApk(); break;
                case "editor": break;
                default: ShowHome(); break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Lunet.Core.ProjectException)
        {
            Toast.MakeText(this, "Não foi possível abrir: " + ex.Message, ToastLength.Long)?.Show();
            LeaveToShell();
        }
    }

    /// <summary>The window got focus back: when the routed screen was a dialog, its last dialog closed — back to the shell.</summary>
    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (!hasFocus || !_routeIsDialog || !_fromShell) return;
        // Give a dialog that opens another one (search, "Usar em…") the moment to take the focus first.
        Window?.DecorView?.PostDelayed(() =>
        {
            if (_routeIsDialog && HasWindowFocus && !IsFinishing) { _routeIsDialog = false; LeaveToShell(); }
        }, 250);
    }

    /// <summary>Saves what is open here (verified writes) and goes back to the shell.</summary>
    private void LeaveToShell()
    {
        SaveCurrent();
        CaptureDocument();
        PersistDocuments();
        DisposePreview();
        Finish();
    }
}
