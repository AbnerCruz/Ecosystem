using Android.App;
using Android.Content;
using Android.Widget;
using AndroidUri = Android.Net.Uri;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>On-demand, consented GitHub cloud build of the currently edited Lunet project.</summary>
public sealed partial class MainActivity
{
    private bool _gameApkBuildBusy;
    private CancellationTokenSource? _gameApkBuildCancel;

    private void AskBuildGameApk()
    {
        if (_project is null || _gameApkBuildBusy) return;
        SaveCurrent();
        var account = _gitAccounts.Load();
        if (string.IsNullOrWhiteSpace(account.Token))
        {
            new AlertDialog.Builder(this)!.SetTitle("Conta GitHub necessária")!
                .SetMessage("Configure seu token no painel Git → Conta Git. O build exige permissões para repositórios privados e workflows (repo + workflow no token clássico).")!
                .SetPositiveButton("Abrir Git", (_, _) => ShowGit())!
                .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
            return;
        }
        var project = _project;
        new AlertDialog.Builder(this)!.SetTitle("Gerar APK do jogo")!
            .SetMessage($"Jogo: {project.Name}\nIdentificador: {project.Manifest.PackageId}\n\n" +
                "O Lunet criará ou usará o repositório PRIVADO 'lunet-game-builds' na sua conta GitHub. " +
                "Seu código e assets serão enviados para lá e compilados pelo GitHub Actions; requer internet e GitHub Actions disponível na conta.\n\n" +
                "O APK terá assinatura de desenvolvimento; uma nova compilação poderá exigir desinstalar a versão anterior. " +
                "O projeto local não será modificado. Não é build offline.")!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Autorizar e gerar", (_, _) => _ = RunCloudGameBuild(project, account.Token))!.Show();
    }

    private async Task RunCloudGameBuild(LunetProject project, string token)
    {
        if (_gameApkBuildBusy) return;
        _gameApkBuildBusy = true;
        _gameApkBuildCancel = new CancellationTokenSource();
        var ct = _gameApkBuildCancel.Token;
        var progress = new AlertDialog.Builder(this)!.SetTitle("Gerando APK")!
            .SetMessage("Preparando o projeto…")!
            .SetCancelable(false)!
            .SetNegativeButton("Cancelar", (_, _) => _gameApkBuildCancel?.Cancel())!.Show();
        try
        {
            if (!Guid.TryParse(project.Manifest.GameId, out var gameId))
                throw new InvalidDataException("GameId inválido em lunet.json.");
            using var snapshot = await Task.Run(() =>
            {
                var buffer = new MemoryStream();
                project.ExportZip(buffer);
                buffer.Position = 0;
                return buffer;
            }, ct);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var client = new GitHubGameBuildClient(http);
            var result = await client.BuildAsync(token, project.Manifest.PackageId, project.Name,
                gameId, snapshot, message => RunOnUiThread(() =>
                {
                    if (!IsFinishing && !IsDestroyed) progress.SetMessage(message);
                }), ct);

            if (IsFinishing || IsDestroyed) return;
            var path = Path.Combine(CacheDir!.AbsolutePath, "lunet-" + gameId.ToString("N") + ".apk");
            await File.WriteAllBytesAsync(path, result.Apk, ct);
            _pendingGameApkPath = path;
            _pendingGameApkName = project.Name + ".apk";
            progress.Dismiss();
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/vnd.android.package-archive");
            intent.PutExtra(Intent.ExtraTitle, _pendingGameApkName);
            StartActivityForResult(intent, GameApkExportRequestCode);
        }
        catch (OperationCanceledException)
        {
            if (!IsFinishing && !IsDestroyed)
                Toast.MakeText(this, "Acompanhamento cancelado. Builds já iniciados podem continuar no GitHub.", ToastLength.Long)?.Show();
        }
        catch (Exception ex)
        {
            if (!IsFinishing && !IsDestroyed)
                new AlertDialog.Builder(this)!.SetTitle("Falha ao gerar APK")!
                    .SetMessage(ex.Message)!.SetPositiveButton("Fechar", (_, _) => { })!.Show();
        }
        finally
        {
            progress.Dismiss();
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
