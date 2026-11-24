using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Lunet.Android.Gles;
using Lunet.Content;
using Lunet.Core;
using Lunet.Storage;

namespace Lunet.Android;

/// <summary>Gameplay uses the existing renderer/input host, not the editor workspace or APK installer.
/// A distinct process contains crashes but has the same Android UID; it is not a sandbox for untrusted code.</summary>
[Activity(Label = "Lunet Jogar", Process = ":gameplay", Exported = false, ExcludeFromRecents = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout)]
public sealed class GameplayActivity : Activity
{
    internal const string SessionExtra = "lunet.gameplay.session";
    internal const string ErrorExtra = "lunet.gameplay.error";
    internal const int RequestCode = 4117;
    private PreviewHost? _host;
    private AndroidGameImagePicker? _picker;
    private int _finished;

    internal static string Staging(Context context) => Path.Combine(context.CacheDir!.AbsolutePath, "gameplay-snapshots");
    internal static string Cache(Context context) => Path.Combine(context.CacheDir!.AbsolutePath, "gameplay-builds");
    internal static string Data(Context context, Guid id) => Path.Combine(context.FilesDir!.AbsolutePath, "gameplay", id.ToString("N"));

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            if (!Guid.TryParseExact(Intent?.GetStringExtra(SessionExtra), "N", out var session))
                throw new InvalidDataException("Sessão de jogo inválida.");
            var root = Path.Combine(Staging(this), session.ToString("N"));
            var manifest = ProjectManifest.Parse(File.ReadAllText(Path.Combine(root, "lunet.json")));
            if (!Guid.TryParse(manifest.GameId, out var id) || id == Guid.Empty)
                throw new InvalidDataException("Identidade do jogo inválida.");
            RequestedOrientation = manifest.Orientation switch
            {
                "landscape" => ScreenOrientation.Landscape,
                "portrait" => ScreenOrientation.Portrait,
                _ => throw new InvalidDataException("Orientação inválida.")
            };
            var data = Data(this, id);
            Directory.CreateDirectory(data);
            var audio = Path.Combine(Cache(this), id.ToString("N"), "audio");
            var saves = Path.Combine(data, "saves");
            Directory.CreateDirectory(saves);
            _picker = new AndroidGameImagePicker(this);
            var renderer = new PreviewRenderer(
                File.ReadAllBytes(Path.Combine(root, "Game.dll")), null,
                new ProjectContentSource(root),
                () => new AndroidAudioBackend(audio, message => global::Android.Util.Log.Error("Lunet.Gameplay", message)),
                new DirectorySaveStore(saves), new AndroidHaptics(this),
                (level, message) => global::Android.Util.Log.Info("Lunet.Gameplay", level + ": " + message),
                _picker, onFatal: message => RunOnUiThread(() => Fail(message)),
                onStarted: () =>
                {
                    try { File.WriteAllText(Path.Combine(data, "last-played"), DateTimeOffset.UtcNow.ToString("O")); }
                    catch (IOException ex) { global::Android.Util.Log.Warn("Lunet.Gameplay", ex.Message); }
                });
            _host = new PreviewHost(this, renderer, () => [], Complete, highRefresh: true, gameplay: true);
            SetContentView(_host);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("Lunet.Gameplay", ex.ToString());
            Fail(ex.Message);
        }
    }

    private void Fail(string message)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0) return;
        using var result = new Intent();
        result.PutExtra(ErrorExtra, message);
        SetResult(Result.Canceled, result);
        Finish();
    }

    private void Complete()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0) return;
        SetResult(Result.Ok);
        Finish();
    }

    public override void OnBackPressed() => Complete();
    protected override void OnPause() { _host?.Pause(); base.OnPause(); }
    protected override void OnResume() { base.OnResume(); _host?.Resume(); }
    protected override void OnDestroy() { _host?.Shutdown(); _host = null; base.OnDestroy(); }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == AndroidGameImagePicker.RequestCode)
            _picker?.Complete(resultCode == Result.Ok ? data?.Data : null);
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e) =>
        _host?.OnKey(keyCode, true, e) == true || base.OnKeyDown(keyCode, e);
    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e) =>
        _host?.OnKey(keyCode, false, e) == true || base.OnKeyUp(keyCode, e);
    public override bool OnGenericMotionEvent(MotionEvent? e) =>
        _host?.OnGenericMotion(e) == true || base.OnGenericMotionEvent(e);
}
