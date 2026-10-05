using Android.App;
using Android.Content;
using Android.Widget;
using Hub.Core.Capabilities;

namespace HubApp;

public sealed partial class MainActivity
{
    Button? _connections;

    void AddIpcControls(LinearLayout content)
    {
        var registry = new Button(this) { Text = "Connections" };
        registry.Click += (_, _) => ShowRegistryConnections();
        content.AddView(registry);

        _connections = new Button(this) { Text = "Conexões IPC" };
        _connections.Click += (_, _) => ShowPendingPairings();
        content.AddView(_connections);
    }

    void ShowRegistryConnections()
    {
        var root = new LocalContext([new("ecosystem", "ecosystem")]);
        var context = new LocalContext([new("ecosystem", "ecosystem"), new("product", "hub")]);
        var host = new LocalCapabilityHost([TextInspectionTool.Definition(root)], []);
        using var session = host.Open("hub-connections-ui", context, []);
        var path = string.Join(" → ", session.Context.Path.Select(step => $"{step.Level}:{step.Id}"));
        var grants = session.Grants.Count == 0 ? "nenhum" : string.Join(", ", session.Grants);
        var lines = new List<string> { $"Context: {path}", $"Grants: {grants}", "" };

        foreach (var connection in session.Connections())
        {
            var required = connection.RequiredPermissions.Count == 0 ? "nenhum" : string.Join(", ", connection.RequiredPermissions);
            lines.Add($"{(connection.Available ? "✓" : "×")} {connection.Capability}@{connection.Version} · {connection.Provider}");
            lines.Add($"  {connection.Status} · {connection.Operation} · lifecycle {connection.Lifecycle} · grants exigidos: {required}");
            if (connection.MissingPermissions.Count > 0)
                lines.Add($"  grants ausentes: {string.Join(", ", connection.MissingPermissions)}");
        }

        lines.Add("");
        lines.Add("Fonte: Registry do Host. Connections não mantém cadastro próprio.");
        new AlertDialog.Builder(this)
            .SetTitle("Connections · Registry")
            .SetMessage(string.Join("\n", lines))
            .SetPositiveButton("Fechar", (_, _) => { })
            .Show();
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
