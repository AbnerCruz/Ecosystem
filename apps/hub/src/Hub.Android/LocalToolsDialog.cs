using Android.App;
using Android.Text;
using Android.Widget;
using Hub.Core.Capabilities;
using System.Text.Json;

namespace HubApp;

/// <summary>Primeiro adapter real local. Texto fica apenas na sessão, sem cache ou rede.</summary>
sealed class LocalToolsDialog(Activity activity)
{
    AlertDialog? _dialog;
    LocalHostSession? _session;
    static readonly LocalContext Scope = new([new("ecosystem", "ecosystem")]);
    static readonly LocalCapabilityHost Host = new([TextInspectionTool.Definition(Scope)], ["ui.display"]);

    public void Show()
    {
        Dismiss();
        var content = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        var padding = ScreenRenderer.Dp(activity, 16);
        content.SetPadding(padding, padding, padding, padding);
        content.AddView(new TextView(activity) { Text = "Cole ou digite um texto. A análise é feita neste aparelho." });
        var input = new EditText(activity) { Hint = "Texto para analisar",
            InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine };
        input.SetMinLines(3); input.SetMaxLines(8);
        input.SetFilters([new InputFilterLengthFilter(TextInspectionTool.MaximumLength)]);
        content.AddView(input);
        var analyze = new Button(activity) { Text = "Analisar texto" };
        content.AddView(analyze);
        var result = new TextView(activity) { TextSize = 16 };
        result.SetTextIsSelectable(true);
        content.AddView(result);
        var dialog = new AlertDialog.Builder(activity).SetTitle("Análise de texto")
            .SetView(content).SetNegativeButton("Fechar", (_, _) => { }).Create()!;
        _dialog = dialog;
        dialog.DismissEvent += (_, _) =>
        {
            if (!ReferenceEquals(_dialog, dialog)) return;
            _session?.Dispose(); _session = null; _dialog = null;
            input.Text = ""; result.Text = "";
        };
        analyze.Click += async (_, _) =>
        {
            if (!analyze.Enabled || !ReferenceEquals(_dialog, dialog)) return;
            _session?.Dispose();
            var context = new LocalContext([new("ecosystem", "ecosystem"), new("product", "hub"),
                new("workspace", Guid.NewGuid().ToString("N"))]);
            // A ação explícita concede somente a exibição já declarada pelo Hub.
            var session = _session = Host.Open("hub-user", context, ["ui.display"]);
            var envelope = new LocalEnvelope(LocalProtocol.Id, Guid.NewGuid().ToString("N"), "hub-user", "request",
                TextInspectionTool.CapabilityId, "inspect", new(1, 0, 0),
                JsonSerializer.SerializeToElement(new { text = input.Text ?? "" }));
            analyze.Enabled = false; result.Text = "Analisando…";
            var response = await Task.Run(() => session.DispatchAsync(envelope));
            if (!ReferenceEquals(_session, session) || !ReferenceEquals(_dialog, dialog)) return;
            analyze.Enabled = true;
            result.Text = response.Output is { } output && response.Succeeded
                ? $"Caracteres: {output.GetProperty("characters").GetInt32()}\nPalavras: {output.GetProperty("words").GetInt32()}\nLinhas: {output.GetProperty("lines").GetInt32()}"
                : response.Error == "CANCELLED" ? "Análise cancelada." : "Não foi possível analisar este texto.";
        };
        dialog.Show();
    }

    public void Dismiss()
    {
        _session?.Dispose(); _session = null;
        _dialog?.Dismiss(); _dialog = null;
    }
}
