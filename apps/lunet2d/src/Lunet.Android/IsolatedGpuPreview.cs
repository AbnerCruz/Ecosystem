using Android.App;
using Android.Content;
using Avalonia.Controls;
using Lunet.Client.Shell;
using Lunet.Compiler;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// M5 with Ajustes → "Preview isolado": the game runs in the separate-process Preview of the previous screens
/// (<see cref="IsolatedPreviewActivity"/>, unchanged), so a game that hangs or runs out of memory never takes the IDE
/// down. The Jogo tab says so while it runs; back in the IDE, <see cref="Collect"/> brings its console in and reports
/// a process that died (the marker file), exactly as the previous screen did.
/// </summary>
internal sealed class IsolatedGpuPreview : IGpuGamePreview
{
    private readonly Activity _activity;

    public IsolatedGpuPreview(Activity activity, CompileResult compiled, LunetProject project)
    {
        _activity = activity;
        var directory = IsolatedPreviewFiles.Directory(activity);
        Directory.CreateDirectory(directory);
        foreach (var stale in new[] { IsolatedPreviewFiles.Log(activity), IsolatedPreviewFiles.Marker(activity), IsolatedPreviewFiles.Symbols(activity), IsolatedPreviewFiles.Profiler(activity) })
            if (File.Exists(stale)) File.Delete(stale);
        File.WriteAllBytes(IsolatedPreviewFiles.Assembly(activity), compiled.Assembly!);
        if (compiled.Symbols is not null) File.WriteAllBytes(IsolatedPreviewFiles.Symbols(activity), compiled.Symbols);
        var intent = new Intent(activity, typeof(IsolatedPreviewActivity));
        intent.PutExtra(IsolatedPreviewFiles.ExtraContent, Path.Combine(project.Directory, "Content"));
        intent.PutExtra(IsolatedPreviewFiles.ExtraSaves, Path.Combine(project.Directory, ".lunet", "saves"));
        intent.PutExtra(IsolatedPreviewFiles.ExtraAudioCache, Path.Combine(activity.CacheDir!.AbsolutePath, "audio"));
        activity.StartActivity(intent);
        View = Lunet.Client.Ui.LunetUi.EmptyState(Lunet.Client.Ui.LunetIcons.Game, "Jogo em processo separado",
            "O Preview isolado está aberto por cima. Se o jogo travar ou faltar memória, o Lunet continua de pé.");
    }

    public Control View { get; }
    public string Backend => "GPU · processo separado";
    public long FramesRendered => 0; // drawn in the other process: not counted here
    // The isolated Preview has its own controls (pause, step, restart, Inspector, profiler): nothing to drive from here.
    public void SetPaused(bool paused) { }
    public void Step() { }
    public void Restart() { }
    public void ToggleInspector() { }
    public void ToggleProfiler() { }
    public void Key(Lunet.Input.Keys key, bool down) { }
    public void SetAppVisible(bool visible) { }
    public void Dispose() { }

    /// <summary>Back in the IDE: the console lines, and the reason when the process died (null when it ended normally).</summary>
    public string? Collect(Action<string> console)
    {
        try
        {
            var log = IsolatedPreviewFiles.Log(_activity);
            if (File.Exists(log))
            {
                foreach (var line in File.ReadAllLines(log))
                {
                    var bar = line.IndexOf('|');
                    if (bar < 1 || !int.TryParse(line[..bar], out var level)) continue;
                    console((level == 2 ? "[erro] " : level == 1 ? "[aviso] " : "") + line[(bar + 1)..].Replace("\\n", "\n"));
                }
                File.Delete(log);
            }
            var marker = IsolatedPreviewFiles.Marker(_activity);
            if (!File.Exists(marker)) return null;
            File.Delete(marker);
            return "O Preview isolado foi encerrado inesperadamente (travou ou faltou memória). O Lunet continua funcionando; corrija o código e rode de novo.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            console("[aviso] Não foi possível ler o console do Preview isolado: " + ex.Message);
            return null;
        }
    }
}
