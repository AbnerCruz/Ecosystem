using Android.App;
using Android.Content;
using Android.Widget;
using AndroidUri = Android.Net.Uri;
using Lunet.Core;
using Lunet.Compiler;

namespace Lunet.Android;

/// <summary>Compilação de jogos standalone no Android, offline, sem credenciais externas.</summary>
public sealed partial class MainActivity
{
    private bool _gameApkBuildBusy;
    private CancellationTokenSource? _gameApkBuildCancel;

    /// <summary>No GitHub, network, login or token: compile C# and package a standalone game APK on-device.</summary>
    private void AskBuildGameApk()
    {
        if (_project is null || _gameApkBuildBusy) return;
        SaveCurrent();
        var project = _project;
        if (!Guid.TryParse(project.Manifest.GameId, out var gameId))
        {
            new AlertDialog.Builder(this)!.SetTitle("Projeto inválido")!
                .SetMessage("GameId inválido em lunet.json.")!.SetPositiveButton("OK", (_, _) => { })!.Show();
            return;
        }
        if (!OfflineGameApkBuilder.ValidPackage(project.Manifest.PackageId))
        {
            new AlertDialog.Builder(this)!.SetTitle("Projeto inválido")!
                .SetMessage("PackageId inválido em lunet.json. Use o formato com.exemplo.meujogo.")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
            return;
        }
        // Um toque no comando começa o build. Sem confirmação, login, token ou internet.
        _ = RunLocalGameBuild(project, gameId);
    }

    private async Task RunLocalGameBuild(LunetProject project, Guid gameId)
    {
        if (_gameApkBuildBusy) return;
        _gameApkBuildBusy = true;
        _gameApkBuildCancel = new CancellationTokenSource();
        var ct = _gameApkBuildCancel.Token;
        string stage = "Preparação do projeto";
        var dialog = new AlertDialog.Builder(this)!.SetTitle("Gerando APK no dispositivo")!
            .SetMessage("Compilando Game.cs…")!
            .SetCancelable(false)!
            .SetNegativeButton("Cancelar", (_, _) => _gameApkBuildCancel?.Cancel())!.Show();
        try
        {
            stage = "Carregamento do runtime Android";
            using var template = Assets!.Open("templates/game-runtime.apk");
            using var templateBuffer = new MemoryStream();
            await template.CopyToAsync(templateBuffer, ct);
            if (templateBuffer.Length < 1024)
                throw new InvalidDataException("Runtime Android do Lunet ausente no pacote.");
            byte[] host = templateBuffer.ToArray();

            // Use the same Roslyn compiler and allowed game references as Run/Preview.
            stage = "Compilação do código C#";
            var sources = project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
            var result = await Task.Run(() => SharedCompiler.Value.Compile(
                "LunetGame" + Guid.NewGuid().ToString("N"), sources), ct);
            if (!result.Success || result.Assembly is null)
            {
                _problems = result.Diagnostics;
                SetPanel(false);
                throw new InvalidDataException($"Jogo não compila: {result.ErrorCount} erro(s). Abra Problemas para ver as linhas.");
            }
            dialog.SetMessage("Empacotando o jogo e seus recursos…");
            var apk = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                stage = "Leitura dos recursos Content/ e Assets/";
                var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["game/Game.dll"] = result.Assembly
                };
                foreach (var relative in project.ListFiles())
                {
                    if (!relative.StartsWith("Content/", StringComparison.Ordinal) &&
                        !relative.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                    ct.ThrowIfCancellationRequested();
                    // Project.ListFiles returns project-owned relative paths. Do not include cache or source.
                    string path = Path.Combine(project.Directory, relative.Replace('/', Path.DirectorySeparatorChar));
                    assets.Add("game/" + relative, File.ReadAllBytes(path));
                }
                stage = "Android Keystore: obtenção da chave de assinatura";
                var signer = new AndroidGameApkSigner(gameId);
                stage = "Empacotamento e assinatura APK v2";
                return OfflineGameApkBuilder.Build(host, assets,
                    project.Manifest.PackageId, project.Name, signer);
            }, ct);

            ct.ThrowIfCancellationRequested();
            stage = "Gravação do APK temporário";
            var dest = Path.Combine(CacheDir!.AbsolutePath, "lunet-" + gameId.ToString("N") + ".apk");
            await File.WriteAllBytesAsync(dest, apk, ct);
            if (IsFinishing || IsDestroyed) return;
            _pendingGameApkPath = dest;
            _pendingGameApkName = project.Name + ".apk";
            dialog.Dismiss();
            stage = "Exportação para o armazenamento Android";
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/vnd.android.package-archive");
            intent.PutExtra(Intent.ExtraTitle, _pendingGameApkName);
            StartActivityForResult(intent, GameApkExportRequestCode);
        }
        catch (OperationCanceledException)
        {
            if (!IsFinishing && !IsDestroyed)
                Toast.MakeText(this, "Geração de APK cancelada.", ToastLength.Short)?.Show();
        }
        catch (Exception ex)
        {
            // Keep the full stack trace in Logcat and identify the failed pipeline step
            // for users; the exception type alone does not locate an Android JNI cast.
            global::Android.Util.Log.Error("Lunet.GameApk", $"Etapa: {stage}\n{ex}");
            if (!IsFinishing && !IsDestroyed)
                new AlertDialog.Builder(this)!.SetTitle("Falha ao gerar APK local")!
                    .SetMessage($"Etapa: {stage}\n{ex.GetType().Name}: {ex.Message}")!
                    .SetPositiveButton("Fechar", (_, _) => { })!.Show();
        }
        finally
        {
            dialog.Dismiss();
            _gameApkBuildCancel?.Dispose();
            _gameApkBuildCancel = null;
            _gameApkBuildBusy = false;
        }
    }

    private void CompleteApkExport(Result resultCode, AndroidUri? uri)
    {
        string? cached = _pendingGameApkPath;
        _pendingGameApkPath = null;
        _pendingGameApkName = null;
        if (resultCode != Result.Ok || uri is null || string.IsNullOrWhiteSpace(cached)) return;
        try
        {
            using (var input = File.OpenRead(cached))
            using (var output = ContentResolver?.OpenOutputStream(uri) ?? throw new IOException("Destino indisponível"))
                input.CopyTo(output);
            try { File.Delete(cached); } catch (IOException) { }
            new AlertDialog.Builder(this)!.SetTitle("APK do jogo salvo")!
                .SetMessage("O APK está no local escolhido e roda sem o Lunet. O Android poderá pedir autorização para instalar. Assinatura beta: evite desinstalar o Lunet antes de preservar a chave, pois isso pode impedir futuras atualizações deste jogo.")!
                .SetPositiveButton("Instalar", (_, _) =>
                {
                    try
                    {
                        var intent = new Intent(Intent.ActionView);
                        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
                        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                        StartActivity(intent);
                    }
                    catch (Exception ex) when (ex is ActivityNotFoundException or global::Java.Lang.SecurityException)
                    { Toast.MakeText(this, "Abra o APK no aplicativo Arquivos: " + ex.Message, ToastLength.Long)?.Show(); }
                })!
                .SetNegativeButton("Fechar", (_, _) => { })!.Show();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or global::Java.Lang.SecurityException)
        {
            new AlertDialog.Builder(this)!.SetTitle("Não foi possível salvar APK")!
                .SetMessage(ex.Message)!.SetPositiveButton("OK", (_, _) => { })!.Show();
        }
    }
}
