using Android.App;
using Android.Widget;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    readonly CancellationTokenSource _ecosystemIpcLifetime = new();
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
        input.ImeOptions = global::Android.Views.InputMethods.ImeAction.Done;

        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(16), 0, Dp(16), 0);
        panel.AddView(input);
        var lifecycle = new Button(this) { Text = "Abrir sessão por 60 s (teste de lifecycle)" };
        lifecycle.SetAllCaps(false);
        lifecycle.Click += async (_, _) =>
        {
            _ecosystemHostClient ??= new EcosystemHostClient(this);
            var result = await _ecosystemHostClient.ConnectAndInspectAsync(
                "", _ecosystemIpcLifetime.Token, TimeSpan.FromSeconds(60),
                () => RunOnUiThread(() =>
                    Toast.MakeText(this,
                        "Sessão IPC aberta por 60 s. Abra o Hub para ver Sessões ativas: 1 ou force a parada do Lunet.",
                        ToastLength.Long)?.Show()));
            if (_ecosystemIpcLifetime.IsCancellationRequested) return;
            RunOnUiThread(() =>
                Toast.MakeText(this, result.Message, ToastLength.Long)?.Show());
        };
        panel.AddView(lifecycle);

        new AlertDialog.Builder(this)
            .SetTitle("Conexão local do Ecosystem")
            .SetMessage("O Product continua funcionando sem provider. No primeiro uso, compare o código de 6 dígitos e aprove a conexão no aplicativo provider.")
            .SetView(panel)
            .SetNegativeButton("Cancelar", (_, _) => { })
            .SetNeutralButton("Esquecer conexão", (_, _) =>
            {
                _ecosystemHostClient ??= new EcosystemHostClient(this);
                var reset = _ecosystemHostClient.ResetTrust();
                Toast.MakeText(this,
                    reset
                        ? "Confiança local apagada e chave girada. O próximo uso exigirá novo pareamento."
                        : "Há uma sessão IPC ativa. Encerre-a antes de esquecer a conexão.",
                    ToastLength.Long)?.Show();
            })
            .SetPositiveButton("Conectar e testar", async (_, _) =>
            {
                _ecosystemHostClient ??= new EcosystemHostClient(this);
                var result = await _ecosystemHostClient.ConnectAndInspectAsync(
                    input.Text ?? "", _ecosystemIpcLifetime.Token);
                if (_ecosystemIpcLifetime.IsCancellationRequested) return;
                new AlertDialog.Builder(this)
                    .SetTitle(result.Success ? "Conexão autenticada" :
                        result.PairCode is null ? "Conexão indisponível" : "Confirme o pareamento")
                    .SetMessage(result.Message)
                    .SetPositiveButton("OK", (_, _) => { })
                    .Show();
            })
            .Show();
    }

    void DisposeEcosystemConnection()
    {
        _ecosystemIpcLifetime.Cancel();
        _ecosystemHostClient?.Dispose();
        _ecosystemHostClient = null;
        _ecosystemIpcLifetime.Dispose();
    }
}
