using Android.App;
using Android.Content;
using Android.Content.PM;
using Hub.Core;
using System.Security.Cryptography;
using System.Text.Json;

namespace HubApp;

internal sealed record InstallOperation(int SessionId, string Token, string PackageId, long VersionCode, string Certificate, bool ConfirmationSent = false);

internal static class HubInstaller
{
    internal static bool Preparing { get; private set; }
    internal static string Message { get; private set; } = "Instalação somente após conferir bytes, pacote e assinatura.";
    static readonly SemaphoreSlim Gate = new(1, 1);
    internal static event Action? Changed;
    internal static string Marker(Context c) => Path.Combine(c.FilesDir!.AbsolutePath, "hub-install.json");
    internal static InstallOperation? Operation(Context c)
    {
        try { return File.Exists(Marker(c)) ? JsonSerializer.Deserialize<InstallOperation>(File.ReadAllText(Marker(c))) : null; }
        catch (Exception) { return null; }
    }
    static void WriteOperation(Context c, InstallOperation op)
    {
        var temp = Marker(c) + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(op));
        File.Move(temp, Marker(c), overwrite: true);
    }
    internal static void SetMessage(string message) { Message = message; Changed?.Invoke(); }
    internal static AndroidPackageEvidence? Installed(Context c, string package)
    {
        try { return Evidence(c.PackageManager!.GetPackageInfo(package, PackageInfoFlags.SigningCertificates)); }
        catch (PackageManager.NameNotFoundException) { return null; }
    }
    static AndroidPackageEvidence? Evidence(PackageInfo? info)
    {
        if (info?.PackageName is null) return null;
        var signers = info.SigningInfo?.GetApkContentsSigners();
        if (signers is null) return null;
        return new(info.PackageName, info.LongVersionCode, info.VersionName,
            signers.Select(s => Convert.ToHexString(SHA256.HashData(s.ToByteArray()!))).ToArray());
    }
    internal static void Recover(Context c)
    {
        if (Preparing || Operation(c) is not { } op) return;
        try
        {
            var session = c.PackageManager!.PackageInstaller!.GetSessionInfo(op.SessionId);
            if (session is not null && session.InstallerPackageName == c.PackageName)
            { SetMessage("Sessão Android pendente; conclua no sistema ou cancele aqui. Nenhuma instalação confirmada."); return; }
            // A missing session is not a success result. Read actual installed state separately in the UI.
            File.Delete(Marker(c));
            SetMessage("Sessão encerrada pelo Android; resultado não recuperado. Consulte a versão realmente instalada.");
        }
        catch (Exception) { SetMessage("Não foi possível reconciliar a sessão Android; nenhuma instalação confirmada."); }
    }
    internal static void Cancel(Context c)
    {
        if (Preparing || Operation(c) is not { } op) return;
        try
        {
            var pi = c.PackageManager!.PackageInstaller!;
            var info = pi.GetSessionInfo(op.SessionId);
            if (info is not null && info.InstallerPackageName != c.PackageName) return;
            if (info is not null) pi.AbandonSession(op.SessionId);
            File.Delete(Marker(c));
            SetMessage("Sessão abandonada. Nenhum app foi desinstalado; confira o estado atual no sistema.");
        }
        catch (Exception) { SetMessage("Android não permitiu abandonar a sessão; confira o instalador do sistema."); }
    }

    internal static async Task BeginAsync(Context context, AndroidProductTrust trust, ArtifactChoice choice,
        ArtifactDownloadResult download, CancellationToken ct, Func<CancellationToken, Task<AndroidProductTrust?>> revalidateTrust)
    {
        var c = context.ApplicationContext!;
        if (!Gate.Wait(0)) return;
        string? privateCopy = null;
        int sessionId = -1;
        bool committed = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (Operation(c) is not null || HubDownloads.Session.Status.Active) throw new InvalidOperationException("Outra operação está ativa.");
            Preparing = true;
            SetMessage("Reconferindo bytes, pacote, assinatura e versão…");
            if (download.State != ArtifactDownloadState.Verified || download.LocalPath != Path.Combine(HubDownloads.Directory(c), "hub-verified.apk"))
                throw new InvalidDataException("Download privado verificado não disponível.");
            privateCopy = Path.Combine(c.CacheDir!.AbsolutePath, $"install-{Guid.NewGuid():N}.apk");
            await using (var input = File.OpenRead(download.LocalPath))
            await using (var output = new FileStream(privateCopy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await InstallationPolicy.CopyVerifiedAsync(input, output, choice.Asset.Size, choice.Asset.Sha256!, ct);
            var candidate = Evidence(c.PackageManager!.GetPackageArchiveInfo(privateCopy, PackageInfoFlags.SigningCertificates));
            var installed = Installed(c, trust.PackageId);
            var verdict = InstallationPolicy.Evaluate(trust, choice, candidate, installed, explicitConsent: true);
            if (!verdict.Allowed) throw new InvalidDataException(verdict.Message);
            if (!c.PackageManager.CanRequestPackageInstalls()) throw new InvalidOperationException("Permissão de instalar desta fonte não concedida. Nenhuma instalação iniciada.");
            var pi = c.PackageManager.PackageInstaller!;
            using var parameters = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
            parameters.SetAppPackageName(trust.PackageId);
            parameters.SetSize(choice.Asset.Size);
            if (OperatingSystem.IsAndroidVersionAtLeast(31)) parameters.SetRequireUserAction(1); // USER_ACTION_REQUIRED
            sessionId = pi.CreateSession(parameters);
            var token = Guid.NewGuid().ToString("N");
            var op = new InstallOperation(sessionId, token, trust.PackageId, candidate!.VersionCode, trust.CertificateSha256);
            WriteOperation(c, op);
            using var session = pi.OpenSession(sessionId);
            await using (var input = File.OpenRead(privateCopy))
            using (var output = session.OpenWrite("base.apk", 0, choice.Asset.Size))
            {
                await InstallationPolicy.CopyVerifiedAsync(input, output!, choice.Asset.Size, choice.Asset.Sha256!, ct);
                session.Fsync(output!);
            }
            ct.ThrowIfCancellationRequested();
            if (await revalidateTrust(ct) != trust) throw new InvalidOperationException("Aprovação/canal mudou ou está indisponível. Sessão abandonada.");
            // Re-read installed version/permission immediately before committing. Do not install over a concurrent update.
            var current = Installed(c, trust.PackageId);
            if (current != installed && (current?.VersionCode != installed?.VersionCode ||
                current is not null && !InstallationPolicy.TrustedPackage(trust, current)))
                throw new InvalidOperationException("App instalado mudou durante a preparação. Tente novamente.");
            if (!c.PackageManager.CanRequestPackageInstalls()) throw new InvalidOperationException("Permissão de instalação revogada.");

            var callback = new Intent(c, typeof(InstallResultReceiver)).SetAction(InstallResultReceiver.Action)
                .SetData(Android.Net.Uri.Parse("hub-install:" + token)).PutExtra("token", token);
            var flags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(31)) flags |= PendingIntentFlags.Mutable;
            using var pending = PendingIntent.GetBroadcast(c, sessionId, callback, flags)!;
            session.Commit(pending.IntentSender!);
            committed = true;
            SetMessage("Enviado ao instalador Android. Aguardando consentimento e resultado real.");
        }
        catch (OperationCanceledException) { SetMessage("Preparação cancelada; nenhuma instalação iniciada."); }
        catch (Exception e) { SetMessage(e is InvalidDataException or InvalidOperationException ? e.Message : "Falha ao preparar instalação; nenhuma instalação confirmada."); }
        finally
        {
            if (!committed && sessionId >= 0)
            {
                try { c.PackageManager!.PackageInstaller!.AbandonSession(sessionId); } catch (Exception) { }
                if (Operation(c)?.SessionId == sessionId) { try { File.Delete(Marker(c)); } catch (Exception) { } }
            }
            if (privateCopy is not null) { try { File.Delete(privateCopy); } catch (Exception) { SetMessage("Falha ao limpar a cópia privada de preparação."); } }
            Preparing = false;
            Gate.Release();
            Changed?.Invoke();
        }
    }

    internal static void Receive(Context c, Intent intent)
    {
        var op = Operation(c);
        if (op is null || intent.Action != InstallResultReceiver.Action || !InstallationResultPolicy.Matches(op.SessionId, op.Token, intent.GetIntExtra(PackageInstaller.ExtraSessionId, -1), intent.GetStringExtra("token"))) return;
        var status = intent.GetIntExtra(PackageInstaller.ExtraStatus, int.MinValue);
        if (status == -1) // STATUS_PENDING_USER_ACTION
        {
            if (op.ConfirmationSent) return;
            var action = intent.GetParcelableExtra(Intent.ExtraIntent) as Intent;
            if (action is null) { SetMessage("Android não forneceu confirmação; cancele a sessão e tente novamente."); return; }
            try { WriteOperation(c, op with { ConfirmationSent = true }); action.AddFlags(ActivityFlags.NewTask); c.StartActivity(action); }
            catch (Exception) { SetMessage("Abra o Hub para acompanhar o instalador do sistema; nenhuma instalação confirmada."); }
            return;
        }
        if (status == int.MinValue) return;
        if (status == 0) // STATUS_SUCCESS must be corroborated by PackageManager, never just the callback.
        {
            var actual = Installed(c, op.PackageId);
            var okay = InstallationResultPolicy.Confirmed(op.PackageId, op.VersionCode, op.Certificate, actual);
            SetMessage(okay ? $"Android confirmou e o pacote instalado foi consultado: {actual!.VersionName} ({actual.VersionCode})."
                : "Android retornou sucesso, mas o pacote não pôde ser confirmado. Consulte o sistema.");
        }
        else SetMessage(status == 3 ? "Instalação cancelada pelo Android/usuário." : $"Android recusou a instalação (código {status}); app anterior preservado.");
        try { File.Delete(Marker(c)); } catch (Exception) { SetMessage("Resultado recebido, mas marcador não pôde ser limpo. Consulte o sistema."); }
    }
}

[BroadcastReceiver(Exported = false)]
public sealed class InstallResultReceiver : BroadcastReceiver
{
    internal const string Action = "io.ecosystem.hub.INSTALL_RESULT";
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null) return;
        try { HubInstaller.Receive(context, intent); }
        catch (Exception) { HubInstaller.SetMessage("Falha ao consultar resultado Android; nenhuma instalação confirmada."); }
    }
}
