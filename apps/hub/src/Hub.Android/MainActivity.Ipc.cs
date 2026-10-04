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
            if (pending is null)
            {
                if (!automatic)
                    new AlertDialog.Builder(this).SetTitle("Conexões locais")
                        .SetMessage("Nenhum pareamento aguardando aprovação. Inicie a conexão no Product caller.")
                        .SetPositiveButton("OK", (_, _) => { }).Show();
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
