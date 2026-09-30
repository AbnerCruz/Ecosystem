using Android.Content;
using Android.Widget;
using Lunet.Compiler;

namespace Lunet.Android;

/// <summary>Lado do IDE do Preview isolado: entrega o jogo compilado ao outro processo e traz de volta o console e o resultado.</summary>
public sealed partial class MainActivity
{
    private void LaunchIsolatedPreview(CompileResult result)
    {
        if (_project is null) return;
        try
        {
            var directory = IsolatedPreviewFiles.Directory(this);
            Directory.CreateDirectory(directory);
            foreach (var stale in new[] { IsolatedPreviewFiles.Log(this), IsolatedPreviewFiles.Marker(this), IsolatedPreviewFiles.Symbols(this) })
                if (File.Exists(stale)) File.Delete(stale);
            File.WriteAllBytes(IsolatedPreviewFiles.Assembly(this), result.Assembly!);
            if (result.Symbols is not null) File.WriteAllBytes(IsolatedPreviewFiles.Symbols(this), result.Symbols);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível preparar o Preview isolado: " + ex.Message, ToastLength.Long)?.Show();
            return;
        }
        SaveCurrent();
        var intent = new Intent(this, typeof(IsolatedPreviewActivity));
        intent.PutExtra(IsolatedPreviewFiles.ExtraContent, System.IO.Path.Combine(_project.Directory, "Content"));
        intent.PutExtra(IsolatedPreviewFiles.ExtraSaves, System.IO.Path.Combine(_project.Directory, ".lunet", "saves"));
        intent.PutExtra(IsolatedPreviewFiles.ExtraAudioCache, System.IO.Path.Combine(CacheDir!.AbsolutePath, "audio"));
        _isolatedRunning = true;
        StartActivity(intent);
    }

    /// <summary>Ao voltar ao IDE: importa o console do Preview isolado e avisa se o processo dele morreu.</summary>
    private void CollectIsolatedPreviewResult()
    {
        if (!_isolatedRunning) return;
        _isolatedRunning = false;
        try
        {
            var log = IsolatedPreviewFiles.Log(this);
            if (File.Exists(log))
            {
                foreach (var line in File.ReadAllLines(log))
                {
                    var bar = line.IndexOf('|');
                    if (bar < 1 || !int.TryParse(line[..bar], out var level)) continue;
                    AppendConsole((LogLevel)Math.Clamp(level, 0, 2), line[(bar + 1)..].Replace("\\n", "\n"));
                }
                File.Delete(log);
            }
            var marker = IsolatedPreviewFiles.Marker(this);
            if (File.Exists(marker))
            {
                File.Delete(marker);
                AppendConsole(LogLevel.Error, "O Preview isolado foi encerrado inesperadamente (travou ou faltou memória). O IDE continua funcionando; corrija o código e aperte Run.");
                _showConsole = true;
                SetPanel(true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppendConsole(LogLevel.Warning, "Não foi possível ler o console do Preview isolado: " + ex.Message);
        }
    }
}
