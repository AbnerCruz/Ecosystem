using Android.App;
using Android.Content;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Ai;
using Lunet.Core;
using System.Security.Cryptography;
using System.Text;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    // The secret is intentionally NOT serialized to the project, chat log, preferences or vault.
    private string _aiEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    private string _aiModel = "";
    private string _aiSecret = "";
    private CancellationTokenSource? _aiRunCancellation;

    private void ShowAiSettings()
    {
        var form = Vertical();
        form.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        var endpoint = new EditText(this) { Text = _aiEndpoint, Hint = "Endpoint HTTPS /chat/completions" };
        endpoint.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        var model = new EditText(this) { Text = _aiModel, Hint = "Modelo (id exato no provider)" };
        var key = new EditText(this) { Text = _aiSecret, Hint = "API key (somente em memória)" };
        key.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        form.AddView(new TextView(this) { Text = "Endpoint HTTPS compatível com /chat/completions (OpenRouter, OpenAI ou servidor compatível)" });
        form.AddView(endpoint);
        form.AddView(new TextView(this) { Text = "Modelo" });
        form.AddView(model);
        form.AddView(new TextView(this) { Text = "Chave de acesso (não é gravada no disco)" });
        form.AddView(key);
        form.AddView(new TextView(this) { Text = "A API pode cobrar pelo uso. Preços e limites financeiros dependem do provider; o Lunet não os mede com precisão nesta versão." });
        var scroll = new ScrollView(this);
        scroll.AddView(form);
        new AlertDialog.Builder(this)!
            .SetTitle("Configurar IA do Lunet")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) =>
            {
                _aiEndpoint = (endpoint.Text ?? "").Trim();
                _aiModel = (model.Text ?? "").Trim();
                _aiSecret = (key.Text ?? "").Trim();
            })!.Show();
    }

    private void ShowAiAssistant()
    {
        if (_project is null)
        {
            Toast.MakeText(this, "Abra um projeto para usar a IA.", ToastLength.Short)?.Show();
            return;
        }
        var project = _project;
        var identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(project.Manifest.GameId)))
            .ToLowerInvariant();
        var historyPath = System.IO.Path.Combine(FilesDir!.AbsolutePath, "ai", identifier + ".json");
        var session = new LunetAiSession(project, historyPath);
        var root = Vertical();
        root.SetPadding(Dp(10), Dp(4), Dp(10), Dp(4));

        root.AddView(new TextView(this)
        {
            Text = "Agente de código · " + project.Name + "\nBusca, leitura por trechos e edição pontual com hash. Apenas leitura por padrão; escrita exige autorização nesta conversa.",
            TextSize = 12
        });
        root.AddView(MakeButton("Configurar provedor, modelo e API key", ShowAiSettings));
        var write = new CheckBox(this)
        {
            Text = "Autorizar criar/alterar arquivos de texto neste projeto",
            Checked = false
        };
        root.AddView(write);
        root.AddView(new TextView(this)
        {
            Text = "O provedor recebe trechos do projeto enviados pelo agente. Revise arquivos confidenciais antes de usar. " +
                   "A IA não compila nem testa automaticamente o código; mantenha um backup do projeto.",
            TextSize = 12
        });
        var transcript = new TextView(this) { TextSize = 13 };
        transcript.SetPadding(Dp(5), Dp(6), Dp(5), Dp(6));
        void DrawTranscript(string? extra = null)
        {
            var entries = session.History.TakeLast(14).Select(t => t.Role + ":\n" + t.Text);
            var content = string.Join("\n\n", entries);
            transcript.Text = (content.Length == 0 ? "Pergunte sobre o código ou peça uma alteração no projeto." : content) +
                (extra is null ? "" : "\n\n" + extra);
        }
        DrawTranscript();
        var transcriptScroll = new ScrollView(this);
        transcriptScroll.AddView(transcript);
        root.AddView(transcriptScroll, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, Dp(260)));
        var input = new EditText(this) { Hint = "O que devemos investigar ou implementar?" };
        input.SetSingleLine(false);
        input.SetMinLines(2);
        input.SetMaxLines(4);
        root.AddView(input);

        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var send = MakeBarButton("Enviar", () => { });
        var cancel = MakeBarButton("Cancelar execução", () => _aiRunCancellation?.Cancel());
        cancel.Enabled = false;
        actions.AddView(send);
        actions.AddView(cancel);
        root.AddView(actions);
        var view = new ScrollView(this);
        view.AddView(root);
        var dialog = new AlertDialog.Builder(this)!
            .SetTitle("IA · Lunet")
            .SetView(view)
            .SetNegativeButton("Fechar", (_, _) => _aiRunCancellation?.Cancel())!
            .Create();
        send.Click += async (_, _) =>
        {
            var request = (input.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(request)) return;
            if (string.IsNullOrWhiteSpace(_aiModel) || string.IsNullOrWhiteSpace(_aiSecret))
            {
                ShowAiSettings();
                Toast.MakeText(this, "Configure o modelo e a chave antes de enviar.", ToastLength.Long)?.Show();
                return;
            }
            // Commit the editor buffer before any model reads/writes disk.
            SaveCurrent();
            send.Enabled = false;
            write.Enabled = false;
            cancel.Enabled = true;
            var cts = new CancellationTokenSource();
            _aiRunCancellation = cts;
            DrawTranscript("Você:\n" + request + "\n\nLunet: executando…");
            try
            {
                var result = await session.AskAsync(request, _aiEndpoint, _aiSecret, _aiModel, write.Checked, cts.Token);
                input.Text = "";
                if (result.ChangedFiles.Count > 0)
                {
                    foreach (var path in result.ChangedFiles.Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                        _assistant?.SetFile(path, project.ReadText(path));
                    if (_openFile is { } opened && result.ChangedFiles.Contains(opened, StringComparer.Ordinal))
                    {
                        _session.Unload();
                        _openFile = null;
                        OpenFile(opened);
                    }
                }
                DrawTranscript(result.Status == AgentRuntime.RunStatus.Succeeded ? null : "A execução terminou sem confirmação de sucesso.");
                if (result.Status != AgentRuntime.RunStatus.Succeeded)
                    transcript.Text += "\n\n" + result.Text;
                if (result.ChangedFiles.Count > 0)
                    transcript.Text += "\n\nArquivos alterados: " + string.Join(", ", result.ChangedFiles);
            }
            catch (OperationCanceledException)
            {
                DrawTranscript("Execução cancelada; verifique os arquivos antes de retomar.");
            }
            catch (Exception ex)
            {
                DrawTranscript("Não foi possível executar: " + ex.Message);
            }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_aiRunCancellation, cts)) _aiRunCancellation = null;
                send.Enabled = true;
                write.Enabled = true;
                cancel.Enabled = false;
                transcriptScroll.Post(() => transcriptScroll.FullScroll(FocusSearchDirection.Down));
            }
        };
        dialog.Show();
    }
}
