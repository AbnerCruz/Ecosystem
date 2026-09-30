using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Lunet.Android.Gles;
using Lunet.Content;
using Lunet.Storage;

namespace Lunet.Android;

/// <summary>Arquivos trocados entre o IDE e o Preview isolado (pasta de cache compartilhada).</summary>
internal static class IsolatedPreviewFiles
{
    public const string ExtraContent = "content";
    public const string ExtraSaves = "saves";
    public const string ExtraAudioCache = "audioCache";

    public static string Directory(Context context) => System.IO.Path.Combine(context.CacheDir!.AbsolutePath, "isolated");

    public static string Assembly(Context context) => System.IO.Path.Combine(Directory(context), "game.dll");

    public static string Symbols(Context context) => System.IO.Path.Combine(Directory(context), "game.pdb");

    /// <summary>Existe enquanto o Preview isolado roda; se sobrar depois que ele fecha, o processo morreu.</summary>
    public static string Marker(Context context) => System.IO.Path.Combine(Directory(context), "running");

    /// <summary>Linhas do console do jogo: "nível|mensagem" (quebras de linha viram \n).</summary>
    public static string Log(Context context) => System.IO.Path.Combine(Directory(context), "log.txt");
}

/// <summary>
/// Preview isolado (§10): roda o jogo em outro processo do Android. Se o jogo travar, estourar a memória ou derrubar o processo,
/// o IDE continua vivo e mostra o que aconteceu.
/// </summary>
[Activity(Label = "Lunet Preview", Process = ":preview", Exported = false, ExcludeFromRecents = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout)]
public sealed class IsolatedPreviewActivity : Activity
{
    private readonly object _logLock = new();
    private PreviewHost? _host;
    private readonly List<string> _tail = [];

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var content = Intent?.GetStringExtra(IsolatedPreviewFiles.ExtraContent);
        var saves = Intent?.GetStringExtra(IsolatedPreviewFiles.ExtraSaves);
        var audio = Intent?.GetStringExtra(IsolatedPreviewFiles.ExtraAudioCache);
        var assemblyPath = IsolatedPreviewFiles.Assembly(this);
        if (content is null || saves is null || audio is null || !File.Exists(assemblyPath))
        {
            Finish();
            return;
        }
        var symbolsPath = IsolatedPreviewFiles.Symbols(this);
        var renderer = new PreviewRenderer(File.ReadAllBytes(assemblyPath), File.Exists(symbolsPath) ? File.ReadAllBytes(symbolsPath) : null,
            new DirectoryContentSource(content), () => new AndroidAudioBackend(audio), new DirectorySaveStore(saves),
            new AndroidHaptics(this), (level, message) => RunOnUiThread(() => Write(level, message)));
        File.WriteAllText(IsolatedPreviewFiles.Marker(this), Process.MyPid().ToString());
        _host = new PreviewHost(this, renderer, () => [], Finish, highRefresh: true);
        SetContentView(_host);
    }

    private void Write(LogLevel level, string message)
    {
        var prefix = level switch { LogLevel.Error => "[erro] ", LogLevel.Warning => "[aviso] ", _ => "" };
        _tail.Add(prefix + message);
        if (_tail.Count > 6) _tail.RemoveAt(0);
        _host?.SetConsole(string.Join('\n', _tail));
        try
        {
            lock (_logLock) File.AppendAllText(IsolatedPreviewFiles.Log(this), $"{(int)level}|{message.Replace("\r", "").Replace("\n", "\\n")}\n");
        }
        catch (IOException)
        {
            // O console em arquivo é auxiliar: perder uma linha não pode derrubar o jogo.
        }
    }

    protected override void OnPause()
    {
        _host?.Pause();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _host?.Resume();
    }

    protected override void OnDestroy()
    {
        _host?.Shutdown();
        // Saída normal: apaga a marca. Se o processo morrer antes disso, a marca fica e o IDE avisa.
        if (IsFinishing)
        {
            try { File.Delete(IsolatedPreviewFiles.Marker(this)); }
            catch (IOException)
            {
                // Sem apagar a marca, o IDE apenas avisa "encerrado inesperadamente" na próxima volta.
            }
        }
        base.OnDestroy();
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e) => _host?.OnKey(keyCode, true, e) == true || base.OnKeyDown(keyCode, e);

    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e) => _host?.OnKey(keyCode, false, e) == true || base.OnKeyUp(keyCode, e);

    public override bool OnGenericMotionEvent(MotionEvent? e) => _host?.OnGenericMotion(e) == true || base.OnGenericMotionEvent(e);

    public override void OnBackPressed() => Finish();
}
