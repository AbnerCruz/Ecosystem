using Android.App;
using Android.Text.InputMethods;
using Android.Widget;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    EcosystemHostClient? _ecosystemHostClient;

    void ShowEcosystemConnection()
    {
        var input = new EditText(this)
        {
            Hint = "Texto para testar text.inspect",
            Text = "Olá do Lunet pelo IPC local."
        };
        input.SetSingleLine(false);
        input.SetMinLines(3);
        input.ImeOptions = ImeAction.Done;

        new AlertDialog.Builder(this)
            .SetTitle("Conexão local do Ecosystem")
            .SetMessage("O Product continua funcionando sem provider. No primeiro uso, compare o código de 6 dígitos e aprove a conexão no aplicativo provider.")
            .SetView(input)
            .SetNegativeButton("Cancelar", (_, _) => { })
            .SetPositiveButton("Conectar e testar", async (_, _) =>
            {
                _ecosystemHostClient ??= new EcosystemHostClient(this);
                var result = await _ecosystemHostClient.ConnectAndInspectAsync(
                    input.Text ?? "", _lifetime.Token);
                if (_destroyed) return;
                new AlertDialog.Builder(this)
                    .SetTitle(result.Success ? "Conexão autenticada" :
                        result.PairCode is null ? "Conexão indisponível" : "Confirme o pareamento")
                    .SetMessage(result.Message)
                    .SetPositiveButton("OK", (_, _) => { })
                    .Show();
            })
            .Show();
    }
}
