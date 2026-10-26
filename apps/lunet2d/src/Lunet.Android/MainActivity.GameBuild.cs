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
        new AlertDialog.Builder(this)!.SetTitle("Gerar APK do jogo")!
            .SetMessage($"Jogo: {project.Name}\nIdentificador: {project.Manifest.PackageId}\n\n" +
                "Compila C# e gera um APK independente neste celular, sem internet, GitHub ou conta. " +
                "A chave de assinatura do jogo fica protegida pelo Android Keystore.\n\n" +
                "ATENÇÃO: desinstalar o Lunet pode apagar essa chave; nesse caso, futuras atualizações " +
                "do jogo exigirão remover o APK antigo. Não use esta assinatura beta para publicar na Play Store.")!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Gerar APK", (_, _) => _ = RunLocalGameBuild(project, gameId))!.Show();
    }

    private async Task RunLocalGameBuild(LunetProject project, Guid gameId)
    {
        if (_gameApkBuildBusy) return;
        _gameApkBuildBusy = true;
        _gameApkBuildCancel = new CancellationTokenSource();
        var ct = _gameApkBuildCancel.Token;
        var dialog = new AlertDialog.Builder(this)!.SetTitle("Gerando APK no dispositivo")!
            .SetMessage("Compilando Game.cs…")!
            .SetCancelable(false)!
            .SetNegativeButton("Cancelar", (_, _) => _gameApkBuildCancel?.Cancel())!.Show();
        try
        {
            using var template = Assets!.Open("templates/game-runtime.apk");
            using var templateBuffer = new MemoryStream();
            await template.CopyToAsync(templateBuffer, ct);
            if (templateBuffer.Length < 1024)
                throw new InvalidDataException("Runtime Android do Lunet ausente no pacote.");
            byte[] host = templateBuffer.ToArray();

            // Use the same Roslyn compiler and allowed game references as Run/Preview.
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
                var signer = new AndroidGameApkSigner(gameId);
                return OfflineGameApkBuilder.Build(host, assets,
                    project.Manifest.PackageId, project.Name, signer);
            }, ct);

            ct.ThrowIfCancellationRequested();
            var dest = Path.Combine(CacheDir!.AbsolutePath, "lunet-" + gameId.ToString("N") + ".apk");
            await File.WriteAllBytesAsync(dest, apk, ct);
            if (IsFinishing || IsDestroyed) return;
            _pendingGameApkPath = dest;
            _pendingGameApkName = project.Name + ".apk";
            dialog.Dismiss();
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
            if (!IsFinishing && !IsDestroyed)
                new AlertDialog.Builder(this)!.SetTitle("Falha ao gerar APK local")!
                    .SetMessage(ex.Message)!.SetPositiveButton("Fechar", (_, _) => { })!.Show();
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
                .SetMessage("O APK está no local escolhido. Instale para executar o jogo sem o Lunet. Caso o Android bloqueie a instalação, autorize esta origem nas configurações.")!
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
