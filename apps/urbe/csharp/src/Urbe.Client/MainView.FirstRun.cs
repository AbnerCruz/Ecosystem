namespace Urbe.Client;

/// <summary>UC-58: the first opening, guided (folder, appearance, Assistente), offered once per device.</summary>
public sealed partial class MainView
{
    private const string FirstRunKey = "urbe.primeiraAbertura";
    private FirstRunView? _firstRun;

    /// <summary>Whether a first opening without a folder offers the guided setup (the real heads; tests turn it on).</summary>
    public bool OffersFirstRun { get; set; } = UrbeApp.LocalStatePath is not null;

    /// <summary>The guided setup (made the first time it shows).</summary>
    public FirstRunView FirstRun
    {
        get
        {
            if (_firstRun is not null) return _firstRun;
            _firstRun = new FirstRunView
            {
                ChooseFolder = async () => { await OpenVaultAsync(); return _snapshot is not null; },
                ChooseTheme = async id =>
                {
                    if (!VaultWritable) return;
                    var result = await Customization.SetAsync(new System.Text.Json.Nodes.JsonObject { ["tema"] = id });
                    if (result.Errors.Count > 0) ShowLifeNotice("O tema não foi guardado: " + result.Errors[0].Message, Ui.ToastKind.Warning);
                }
            };
            _firstRun.Finished += connect =>
            {
                Customization.Device.Set(FirstRunKey, "1");
                if (connect) { Assistant.Open(); Assistant.ShowSettings(); }
            };
            _stage.Children.Add(_firstRun);
            return _firstRun;
        }
    }

    /// <summary>Configurações → Configuração inicial, or the first opening with no folder yet.</summary>
    public void ShowFirstRun() => FirstRun.Start(_snapshot is null ? 0 : 1);

    private void OfferFirstRun()
    {
        if (OffersFirstRun && _snapshot is null && Customization.Device.Get(FirstRunKey) is null) ShowFirstRun();
    }
}
