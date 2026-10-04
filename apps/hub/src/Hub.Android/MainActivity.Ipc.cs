using Android.App;
using Android.Content;
using Android.Widget;

namespace HubApp;

public sealed partial class MainActivity
{
    Button? _connections;

    void AddIpcControls(LinearLayout content)
    {
        _connections = new Button(this) { Text = "Conexões locais" };
        _connections.Click += (_, _) => ShowPendingPairings();
        content.AddView(_connections);
    }

    void PromptPendingIpcPairing()
    {
        if (_destroyed) return;
        ShowPendingPairings(automatic: true);
    }

    void ShowPendingPairings(bool automatic = false)
    {
        try
        {
            var key = new AndroidInstallationKey("ecosystem.ipc.provider.v1");
            var store = new EcosystemPairingStore(this, key);
            var pending = store.Pending().FirstOrDefault();
            var approvedPeers = store.Approved().ToArray();
            var activeSessions = EcosystemCapabilityService.ActiveSessionCount;
            if (pending is null)
            {
                if (automatic) return;
                var approved = approvedPeers.FirstOrDefault();
                if (approved is null)
                {
                    new AlertDialog.Builder(this).SetTitle("Conexões locais")
                        .SetMessage($"Instalações pareadas: 0\nSessões IPC abertas agora: {activeSessions}.\n\nSessões são temporárias e só permanecem abertas enquanto há uma operação ou teste de lifecycle em andamento.")
                        .SetPositiveButton("OK", (_, _) => { }).Show();
                    return;
                }

                var approvedSigner = approved.SignerSha256.Length > 16 ? approved.SignerSha256[..16] + "…" : approved.SignerSha256;
                new AlertDialog.Builder(this).SetTitle("Instalação pareada")
                    .SetMessage($"Aplicativo: {approved.PackageName}\nAssinante observado: {approvedSigner}\nInstalações pareadas: {approvedPeers.Length}\nSessões IPC abertas agora: {activeSessions}\n\nZero sessões é normal quando nenhum comando está em execução. O pareamento continua válido. A revogação remove a confiança desta instalação e o próximo uso exigirá novo pareamento.")
                    .SetNegativeButton("Fechar", (_, _) => { })
                    .SetPositiveButton("Revogar", (_, _) => store.Revoke(approved.PairId))
                    .Show();
                return;
            }

            var signer = pending.SignerSha256.Length > 16 ? pending.SignerSha256[..16] + "…" : pending.SignerSha256;
            new AlertDialog.Builder(this)
                .SetTitle("Aprovar conexão local?")
                .SetMessage($"Código: {pending.Code}\n\nAplicativo: {pending.PackageName}\nAssinante observado: {signer}\n\nCompare o mesmo código no outro aplicativo. Aprovar vincula esta instalação, não concede capabilities além do Context/grants.")
                .SetNegativeButton("Reprovar", (_, _) => store.Reject(pending.PairId))
                .SetPositiveButton("Aprovar", (_, _) => store.Approve(pending.PairId))
                .Show();
        }
        catch (Exception)
        {
            if (!automatic)
                new AlertDialog.Builder(this).SetTitle("Conexões locais")
                    .SetMessage("Não foi possível ler o estado de pareamento. Nenhuma confiança foi concedida.")
                    .SetPositiveButton("OK", (_, _) => { }).Show();
        }
    }
}
